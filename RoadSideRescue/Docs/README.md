# RoadSideRescue

Backend API for a lightweight roadside assistance system — built with .NET 8, EF Core and SignalR. Provides user authentication, agent management, roadside requests and realtime messaging between owners and agents.

Key features
- JWT authentication (HMAC symmetric keys; accepts base64 or plain text)
- EF Core with automatic migrations on startup; supports PostgreSQL and SQLite
- SignalR hub at `/hubs/requests` for realtime events (requests, assignments, messages, agent locations)
- Swagger UI enabled in Development
- Development seed data (test user, agent, request, message)

Quick links
- API docs: `API.md`
- SignalR hub: `/hubs/requests`
- Dev seed credentials: `test@example.com` / `Password123!` (Development only)

Requirements
- .NET 8 SDK
- Visual Studio 2022 or newer (or `dotnet` CLI)
- Optional: EF Core tools (`dotnet-ef`) for manual migrations

Quick start (development)
1. Clone the repo and open the solution in Visual Studio 2022 or use CLI:
   - Prefer storing secrets using Visual Studio's __Manage User Secrets__ or environment variables.
2. Configure environment or secrets:
   - Required: `Jwt__Key`, `Jwt__Issuer`, `Jwt__Audience`, `ConnectionStrings__DefaultConnection`.
   - The app accepts either a base64-encoded JWT key or plain text. The decoded key must be at least 16 bytes.
3. Database:
   - If your `DefaultConnection` contains `Host=` or `Server=` the app uses PostgreSQL (Npgsql); otherwise SQLite.
4. Run:
   - Visual Studio: Run the project.
   - CLI: `dotnet run --project RoadSideRescue`
   - On startup the app applies migrations and (in Development) seeds sample data.
5. Swagger (Development): `https://localhost:{port}/swagger`

Configuration notes
- JWT key must be at least 128 bits (16 bytes). If using base64, ensure it decodes to >=16 bytes.
- For local development prefer using __Manage User Secrets__ or environment variables to avoid committing secrets.
- CORS policy `AllowLocalhost` permits `http://localhost:3000` and `https://localhost:3000`.

SignalR (Realtime)
- Hub endpoint: `/hubs/requests`
- The server accepts JWT via:
  - `Authorization: Bearer <token>` for transports that support headers
  - `?access_token=<token>` query parameter for client websockets (server reads `access_token` for `/hubs/requests`)
- Common server-invoked client events: `RequestCreated`, `RequestAssigned`, `NewMessage`, `AgentLocationUpdate`, `Error`.

Testing
- Run unit and integration tests:
  - CLI: `dotnet test`
  - Visual Studio: Run tests via Test Explorer

Project layout (top-level)
- `Program.cs` — startup, DI, JWT setup, migration & seeding
- `Controllers/` — API controllers (Auth, Requests, Agents, Uploads)
- `Hubs/RequestsHub.cs` — SignalR hub
- `Services/` — business logic and repositories
- `Data/ApplicationDbContext.cs` — EF Core DbContext
- `Models/` — domain entities (User, Agent, Request, Message)
- `Dto/` — request/response DTOs
- `Docs/` — additional docs (source for these files)

Contributing
- Add `[Authorize]` to controllers/actions that must require authentication.
- Use JWT `sub` claim to obtain authenticated user id.
- Keep secrets out of source control. Use __Manage User Secrets__ or environment variables.

License
- No license file included. Add a `LICENSE` in the repo if needed.