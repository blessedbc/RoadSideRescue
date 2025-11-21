# API Documentation — RoadSideRescue Backend

Base URL (development): `https://localhost:{port}/api/`

Authentication: JWT Bearer tokens. Token generation endpoints are under `/api/auth/*`. The API uses standard Authorization header `Authorization: Bearer {token}`. For SignalR, the server also reads `access_token` query parameter for connections to `/hubs/requests`.

Important: many controllers in this project currently do not require `[Authorize]` attributes, but the authentication infrastructure is configured. Behavior may vary based on controller implementation.

---

## Authentication

### POST /api/auth/register
Register a new user.

Request body (JSON)
- `name` (string) — user display name
- `email` (string) — required
- `phone` (string) — optional
- `password` (string) — required
- `role` (int or string) — user role enum (project uses `UserRole` model)

Response
- `201 Created` with body:
  - `userId` (GUID)
  - `token` (JWT)
- `400 Bad Request` for validation errors
- `409` or `400` if user exists (implementation throws InvalidOperationException)
- `500` on server errors

Example
curl:
curl -X POST https://localhost:{port}/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"name":"Alice","email":"alice@example.com","password":"Password123!","phone":"1234567890","role":0}'

---

### POST /api/auth/login
Login and receive JWT.

Request body
- `email` (string)
- `password` (string)

Response
- `200 OK` with body: `{ "token": "<jwt>" }`
- `401 Unauthorized` if credentials invalid
- `400` for validation errors
- `500` on server error

Example
curl:
curl -X POST https://localhost:{port}/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"Password123!"}'

---

## Requests

### POST /api/requests
Create a new roadside request.

Request body (CreateRequestDto)
- `lat` (double) — required
- `lng` (double) — required
- `address` (string) — optional
- `description` (string) — optional
- `vehicleType` (string) — optional
- `photos` (string[]) — optional

Notes:
- Current controller implementation sets `OwnerId` to a new GUID (placeholder). When connecting with authenticated users, modify controller to use the authenticated user's ID from JWT claims.

Response
- `201 Created` with body `{ id, status }`
- `400 Bad Request` if DTO missing or invalid

Server side realtime behavior:
- Broadcasts `RequestCreated` to all SignalR clients with `{ Id, Status, Lat, Lng }`
- Adds owner to a SignalR group for the request so owner can receive group messages

Example
curl:
curl -X POST https://localhost:{port}/api/requests \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <token>" \
  -d '{"lat":-33.9258,"lng":18.4232,"address":"Cape Town","description":"Car stuck","vehicleType":"Sedan"}'

---

### GET /api/requests/{id}
Get request summary.

Response `200 OK` body (RequestSummaryDto)
- `id` (GUID)
- `status` (string)
- `lat` (double)
- `lng` (double)
- `address` (string)
- `description` (string)
- `vehicleType` (string)
- `photos` (string[])

Errors
- `404 Not Found` if missing

---

## Agent endpoints

### POST /api/agent/availability
Update agent availability (implementation placeholder).

Request body (AgentAvailabilityRequest) — expected fields:
- `agentId` (GUID)
- `available` (bool)
- `lat` (double)
- `lng` (double)
- `serviceRadiusMeters` (int)
- `skills` (string[])

Response
- `200 OK` with `{ success: true }` on success
- `400/500` on errors

### POST /api/agent/{requestId}/accept
Agent accepts a request.

Path parameter
- `requestId` (GUID)

Response
- `200 OK` `{ assigned: true }` on success
- `400 Bad Request` for invalid request id
- `409 Conflict` if business rules prevented assignment
- `500` on server error

Note: When a request is accepted via the hub or controller, the request status is updated and the hub notifies the request group.

---

## SignalR Hub — Realtime (RequestsHub)

Hub endpoint: `/hubs/requests`

Connection
- For standard Bearer header use WebSockets with the `Authorization` header when supported.
- For browsers where the header cannot be set for the WebSocket handshake, include the JWT as a query string parameter `access_token` when connecting to `/hubs/requests`. The server explicitly reads `access_token` for paths that start with `/hubs/requests`.

Client-side method calls (server methods):
- `JoinRequestGroup(Guid requestId, Guid userId)` — add connection to request group
- `AcceptRequest(Guid requestId, Guid agentId)` — agent accepts a request (updates DB and notifies group)
- `SendMessage(Guid requestId, Guid fromUserId, string message)` — store and broadcast message to the request group
- `UpdateLocation(Guid agentId, double lat, double lng)` — update agent location and broadcast `AgentLocationUpdate`

Server-sent events
- `RequestCreated` — broadcast to all clients when a request is created. Payload: `{ Id, Status, Lat, Lng }`
- `RequestAssigned` — sent to request group when an agent is assigned. Payload: `{ Id, Status, AssignedAgentId }`
- `NewMessage` — sent to request group when a new message is posted. Payload: `{ Id, RequestId, FromUserId, Text, CreatedAt }`
- `AgentLocationUpdate` — broadcast to all clients. Payload: `{ Id, CurrentLat, CurrentLng }`
- `Error` — sent to caller when problems occur

Example JS client connect (using @microsoft/signalr):
const connection = new signalR.HubConnectionBuilder()
  .withUrl("/hubs/requests?access_token=<JWT>")
  .build();

connection.on("RequestCreated", data => console.log(data));
await connection.start();

---

## Models (fields)
These are the primary domain models (simplified):

User
- `Id` (GUID)
- `Name` (string)
- `Email` (string)
- `Phone` (string)
- `PasswordHash` (string)
- `Role` (enum)
- `RatingAverage` (double)
- `CreatedAt` (DateTime)

Agent
- `Id` (GUID)
- `UserId` (GUID)
- `Status` (enum)
- `CurrentLat` (double?)
- `CurrentLng` (double?)
- `LastSeenAt` (DateTime?)
- `ServiceRadiusMeters` (int)
- `SkillsJson` (string)

Request
- `Id` (GUID)
- `OwnerId` (GUID)
- `Status` (enum)
- `CreatedAt` (DateTime)
- `UpdatedAt` (DateTime?)
- `Lat` (double)
- `Lng` (double)
- `Address` (string)
- `Description` (string)
- `VehicleType` (string)
- `Severity` (int?)
- `AssignedAgentId` (GUID?)
- `EtaMinutes` (int?)
- `Photos` (List<string>)

Message
- `Id` (GUID)
- `RequestId` (GUID)
- `FromUserId` (GUID)
- `Text` (string)
- `CreatedAt` (DateTime)

---

## Error Handling & Status codes
- Controllers return `400` for invalid input, `404` for not found, `401` for authentication issues, `409` for conflicts, `500` for server errors. See each controller method for specifics.

---

## Development seed (useful testing credentials)
When running in Development, the app will seed the DB with:
- User: `test@example.com` / `Password123!`
- An agent linked to that user
- One sample request assigned to that agent
- One sample message

---

## Contributing & next steps
- Add `[Authorize]` attributes to controllers/actions that should require authentication.
- Use claims from JWT to identify the authenticated user (`sub` claim contains user id).
- Expand repositories and services for agent matching, ETA calculation, push notifications etc.
- Add comprehensive OpenAPI docs for DTOs and models (Swagger annotations).
