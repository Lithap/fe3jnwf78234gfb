# RetroRec Server

Private server implementation for the RecNet platform. Built on ASP.NET Core 8 with a SQLite persistence layer and SignalR for real-time client communication.

---

## Stack

| Layer | Technology |
|---|---|
| Runtime | .NET 8 |
| Framework | ASP.NET Core (Minimal API + Controllers) |
| Real-time | SignalR (`/hub/v1`) |
| Database | SQLite via EF Core 8 |
| Reverse proxy | Forwarded headers (nginx / ngrok compatible) |

---

## Endpoints

| Method | Path | Description |
|---|---|---|
| `POST` | `/eac/challenge` | Anti-cheat challenge handshake |
| `POST` | `/connect/token` | Token issuance |
| `GET` | `/account/me` | Authenticated account lookup |
| `GET` | `/api/avatar/v2` | Avatar data |
| `GET` | `/api/communityboard/v2/current` | Community board |
| `WS` | `/hub/v1` | SignalR hub (player subscriptions) |

---

## Getting Started

**Prerequisites:** .NET 8 SDK

```bash
git clone https://github.com/Lithap/fe3jnwf78234gfb
cd fe3jnwf78234gfb
dotnet restore
dotnet build
```

**Run:**

```bash
dotnet run
```

The server binds to `0.0.0.0:80` and `0.0.0.0:2059` by default. To override:

```bash
ASPNETCORE_URLS="http://127.0.0.1:5080" dotnet run --no-build
```

The SQLite database (`retrorec.db`) is created automatically on first startup. The schema bootstraps itself — no migration commands required.

---

## Database Schema

Three tables are provisioned on startup in addition to the core EF Core schema:

```
Bios                — Per-account biography text with update timestamp
FriendRelationships — Directional friend request records (sender → target)
PlayerCheers        — One-per-category cheer records between accounts in a room
```

EF Core's `EnsureCreated` handles the initial schema. New tables added post-release are bootstrapped with idempotent `CREATE TABLE IF NOT EXISTS` statements, making the process safe for both fresh installs and existing databases.

---

## Configuration

No config file is required for a basic run. The following environment variables are respected:

| Variable | Effect |
|---|---|
| `ASPNETCORE_URLS` | Override the default bind addresses |
| `ASPNETCORE_ENVIRONMENT` | Set to `Development` for detailed error output |

Reverse proxy deployments (nginx, Cloudflare Tunnel, ngrok) are supported out of the box. `X-Forwarded-For`, `X-Forwarded-Proto`, and `X-Forwarded-Host` headers are trusted. Scope the trusted proxy list in `Program.cs` if the server is internet-facing.

---

## Project Structure

```
├── Controllers/          # API route handlers
│   └── ConfigController.cs   (excluded from build — see note below)
├── Models/               # EF Core entity models and DbContext
├── RecNetHub.cs          # SignalR hub — player subscription events
└── Program.cs            # Host configuration, middleware pipeline, schema bootstrap
```

> **ConfigController.cs** is explicitly excluded from compilation in the `.csproj`. It contains environment-specific configuration that is not suitable for general builds. Do not remove the exclusion without reviewing its contents first.

---

## SignalR Hub

The hub is mounted at `/hub/v1`. Two client-invokable methods are exposed:

| Method | Description |
|---|---|
| `SubscribeToPlayers` | Register interest in player state updates |
| `UnsubscribeFromPlayers` | Deregister player state subscription |

Connection and disconnection events are logged to stdout with the SignalR connection ID.

---

## Notes

- All unhandled exceptions are caught at the middleware level and return `HTTP 200 {}` to the client. Crash details are written to stdout. Monitor the process log — status codes alone will not surface errors.
- The response body buffer middleware logs payloads under 500 characters. Larger bodies are noted by byte count only.
- Port `2059` is the secondary bind address. Its purpose is deployment-specific.

---

## License

Private. Not licensed for redistribution.
