# RoadSideRescue — API Reference

Base URL (development): `https://localhost:{port}/api/`

Authentication
- JWT Bearer tokens. Obtain tokens via `/api/auth/*`.
- Use `Authorization: Bearer <token>` header.
- For SignalR connections to `/hubs/requests`, you may provide the token as `?access_token=<token>`.

JWT notes
- Tokens include:
  - `sub` — user id (GUID)
  - `role` — role name (e.g., `Agent`, `Owner`)
- Server requires JWT key length >= 16 bytes (after base64 decode if provided).

Auth
- POST `/api/auth/register`
  - Body: `{ "name", "email", "phone?", "password", "role?" }`
  - Success: `201 Created` with `{ userId, token }`
- POST `/api/auth/login`
  - Body: `{ "email", "password" }`
  - Success: `200 OK` with `{ token }`

Requests
- POST `/api/requests`
  - Create a new roadside request
  - Body (CreateRequestDto): `{ "lat":double, "lng":double, "address?" , "description?" , "vehicleType?" , "photos?" }`
  - Requires authentication for owner-specific behavior (controllers may use placeholder owner id in current implementation)
  - Response: `201 Created` with `{ id, status }`
  - Side-effect: broadcasts `RequestCreated` to SignalR clients
- GET `/api/requests/{id}`
  - Get request summary
  - Response: `200 OK` with `RequestSummaryDto` fields: `id, status, lat, lng, address, description, vehicleType, photos`
- (Controller contains additional request-related endpoints — inspect `Controllers/RequestsController.cs` for details)

Agents
- POST `/api/agent/availability`
  - Update agent availability & location (implementation expects fields such as `agentId`, `available`, `lat`, `lng`, `serviceRadiusMeters`, `skills`)
  - Returns `200 OK` or `400/500` on error
- POST `/api/agent/{requestId}/accept`
  - Agent accepts a request (path `requestId`)
  - Returns `200 OK` on success; hub broadcast is emitted when assignment completes

SignalR — Realtime
- Hub endpoint: `/hubs/requests`
- Client -> Server methods (examples)
  - `JoinRequestGroup(Guid requestId, Guid userId)` — add connection to request group
  - `AcceptRequest(Guid requestId, Guid agentId)` — agent accepts a request
  - `SendMessage(Guid requestId, Guid fromUserId, string message)` — persist and broadcast message
  - `UpdateLocation(Guid agentId, double lat, double lng)` — update location and broadcast
- Server -> Client events (payloads)
  - `RequestCreated` — `{ Id, Status, Lat, Lng }`
  - `RequestAssigned` — `{ Id, Status, AssignedAgentId }`
  - `NewMessage` — `{ Id, RequestId, FromUserId, Text, CreatedAt }`
  - `AgentLocationUpdate` — `{ Id, CurrentLat, CurrentLng }`
  - `Error` — arbitrary error payload to caller

SignalR client examples
- JavaScript (using @microsoft/signalr)
  - Connect with query token:
    const connection = new signalR.HubConnectionBuilder()
      .withUrl("/hubs/requests?access_token=<JWT>")
      .build();
    connection.on("RequestCreated", data => console.log("RequestCreated", data));
    await connection.start();

- Testing with .NET client
  - The integration tests show using `HubConnectionBuilder().WithUrl(hubUrl, options => options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler())` to route to test server, and subscribing to `connection.On<object>("EventName", handler)`.

Models (summary)
- User: `{ Id: GUID, Name, Email, Phone, PasswordHash, Role, RatingAverage, CreatedAt }`
- Agent: `{ Id, UserId, Status, CurrentLat?, CurrentLng?, LastSeenAt?, ServiceRadiusMeters, SkillsJson }`
- Request: `{ Id, OwnerId, Status, CreatedAt, UpdatedAt?, Lat, Lng, Address, Description, VehicleType, Severity?, AssignedAgentId?, EtaMinutes?, Photos[] }`
- Message: `{ Id, RequestId, FromUserId, Text, CreatedAt }`

Errors & status codes
- `400` Bad Request — invalid input
- `401` Unauthorized — missing/invalid token
- `404` Not Found — resource missing
- `409` Conflict — business rule conflict
- `500` Server error

Development seed
- When running in Development the app seeds:
  - `test@example.com` / `Password123!`
  - One agent attached to that user
  - One request assigned to that agent
  - One sample message

Testing
- Run the test suite with `dotnet test` (Solution root).

Notes & next steps
- Add `[Authorize]` attributes to controllers that must be protected.
- Use JWT `sub` claim to get authenticated user's id when creating requests or messages.
- Consider adding OpenAPI annotations for DTOs to improve Swagger docs.
