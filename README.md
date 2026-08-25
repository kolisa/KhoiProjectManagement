# Khoi Pro

A full-stack company system-of-record: ASP.NET Core 10 Web API + PostgreSQL backend, React 18 +
Tailwind frontend. See [CLAUDE.md](CLAUDE.md) for the full architecture writeup.

This README covers **local setup**: getting the database, API, and frontend running, and how they
connect to each other.

## Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (for Postgres, and optionally the API)
- [.NET 10 SDK](https://dotnet.microsoft.com/download) (if you're running the API with `dotnet run` instead of in Docker)
- [Node.js 22+](https://nodejs.org/) and npm (for the frontend)

## Quick start (recommended): Postgres in Docker, API with `dotnet run`

This is the fastest inner loop for actually working on the API - the container only holds the database.

```bash
# 1. Start Postgres (creates an empty "ProjectManagementDB" database, nothing else yet)
docker compose up postgres -d

# 2. Run the API - it creates the schema and seed data itself on startup (see "How the database gets
#    created" below), no separate migration step needed
cd KhoiProjectManagementApi
dotnet run
```

The API is now listening on `http://localhost:5278` (Swagger UI at `http://localhost:5278/swagger`).

```bash
# 3. In a second terminal, start the frontend
cd KhoiProjectManagementApp
npm install
npm run dev
```

The frontend is now at `http://localhost:3000` and already points at the API above (see "How the
frontend connects to the API" below) - no config changes needed for this path.

## Alternative: run the API in Docker too

If you don't have (or don't want to install) the .NET SDK locally, or you want the API running as a
container the same way it would in a real deployment:

```bash
docker compose up -d
```

This starts **both** `postgres` and `api`. The API container builds from
[`KhoiProjectManagementApi/Dockerfile`](KhoiProjectManagementApi/Dockerfile) and is reachable at the
same `http://localhost:5278` as the `dotnet run` path above - **don't run both at once**, they'd fight
over that port.

Then start the frontend the same way as step 3 above (`cd KhoiProjectManagementApp && npm install &&
npm run dev`).

To stop everything (keeping the database volume, so your data survives):

```bash
docker compose down
```

To wipe the database and start completely fresh:

```bash
docker compose down -v
```

## How the database gets created

There's no manual `dotnet ef database update` step for local dev. On every startup, the API
(`Program.cs`) automatically:

1. Runs `Database.MigrateAsync()` - creates the `ProjectManagementDB` schema from scratch (all tables)
   if it doesn't exist yet, or applies any migrations it hasn't seen yet if it does.
2. Runs `DatabaseSeeder.SeedAsync()` - inserts the default users/roles/permissions listed below, but
   only the *first* time (it checks `if (await context.Users.AnyAsync()) return;` first, so it's a
   no-op on every later restart).

This is true whether you run the API with `dotnet run` or in the `api` Docker container - same
`Program.cs`, same behavior either way.

You'll see a `[ERR] Failed executing DbCommand ... SELECT "MigrationId" ... FROM "__EFMigrationsHistory"`
line in the logs the very first time you point the API at a brand-new database - that's EF Core
checking for its own bookkeeping table before it exists yet, expected on a first run, not a real
failure (migrations apply successfully right after it).

### Seeded users (all created automatically - see above)

| Email | Password | Role |
|---|---|---|
| `kholisa@khoitech.Africa` | `admin123` | Admin (all permissions) |
| `seati@khoitech.Africa` | `manager123` | Manager |
| `kenneth@khoitech.Africa` | `member123` | Member |
| `thato@khoitech.Africa` | `member123` | Member |
| `metsing@khoitech.Africa` | `member123` | Member |
| `Relebohile@khoitech.Africa` | `member123` | Member |

## How the frontend connects to the API

The frontend reads `VITE_API_URL` at build/dev time (see `KhoiProjectManagementApp/.env.local`, which
already points at `http://localhost:5278/api` - matching both the `dotnet run` and Docker paths above).
If you ever need to point it somewhere else, edit that file's `VITE_API_URL` and restart `npm run dev`.

The API's CORS policy (`Cors:AllowedOrigins` in `appsettings.json`) explicitly allows
`http://localhost:3000` - if you change the frontend's dev port, update both `vite.config.js`'s
`server.port` and that CORS list, or requests will be blocked by the browser.

## Connecting with something other than the frontend (Swagger, Postman, curl)

1. Get a token:
   ```bash
   curl -X POST http://localhost:5278/api/auth/login \
     -H "Content-Type: application/json" \
     -d '{"email":"kholisa@khoitech.Africa","password":"admin123"}'
   ```
2. Use the returned `token` as a Bearer token on every subsequent request:
   ```bash
   curl http://localhost:5278/api/projects \
     -H "Authorization: Bearer <token>"
   ```

Swagger UI (`http://localhost:5278/swagger`) has a padlock/"Authorize" button that does the same
thing - paste just the raw token (no `Bearer ` prefix needed there).

## Connection strings, by context

| Running the API... | Postgres host:port |
|---|---|
| via `dotnet run` on your machine | `localhost:5433` (host port docker-compose.yml maps to Postgres's container port 5432) |
| via the `api` Docker container | `postgres:5432` (Docker Compose's internal network - overridden via the `ConnectionStrings__DefaultConnection` environment variable in `docker-compose.yml`, since `localhost` means something different *inside* a container) |

`appsettings.json`'s committed `ConnectionStrings:DefaultConnection` is the `dotnet run` one; you don't
need to (and shouldn't) edit it for the Docker path - the environment variable override handles that.

## Ports at a glance

| Service | Port | Notes |
|---|---|---|
| Frontend (`npm run dev`) | 3000 | |
| API (`dotnet run` or Docker) | 5278 | Swagger at `/swagger` |
| Postgres | 5433 (host) → 5432 (container) | Not 5432 directly, in case you have a native Postgres install already using it |

## Known committed secrets (local dev only)

`KhoiProjectManagementApi/appsettings.json` has real-looking JWT/SMTP/DB credentials committed
directly - this is intentional for local dev convenience in this repo, but treat it as sensitive and
never reuse these values for a real deployment.

## Deploying to a server

Don't put a real server's credentials into `appsettings.json` or `appsettings.Development.json` -
both are committed to git. Instead, ASP.NET Core layers `appsettings.{ASPNETCORE_ENVIRONMENT}.json` on
top of `appsettings.json` automatically, so real per-environment secrets go in
`appsettings.Production.json` instead - gitignored, so it only ever exists on machines that actually
need it (never in the repo's history).

1. On the server, copy the tracked template and fill in real values:
   ```bash
   cp KhoiProjectManagementApi/appsettings.Production.json.example KhoiProjectManagementApi/appsettings.Production.json
   # then edit ConnectionStrings:DefaultConnection, Jwt:SecretKey, App:FrontendBaseUrl, Cors:AllowedOrigins
   ```
2. Apply the schema **before** the first start - `App:AutoMigrateOnStartup` is `false` in the template
   (see below for why), so nothing does this for you here the way `dotnet run`/the Docker path do
   locally:
   ```bash
   cd KhoiProjectManagementApi
   dotnet ef database update --project ..\KhoiProjectManagement.Infrastructure --startup-project . \
     --connection "<the same connection string you put in appsettings.Production.json>"
   ```
3. Run with `ASPNETCORE_ENVIRONMENT=Production` so `appsettings.Production.json` actually gets picked up:
   ```bash
   ASPNETCORE_ENVIRONMENT=Production dotnet KhoiProjectManagementApi.dll
   ```
   (or, for the Docker path, pass it as an environment variable to the `api` service/container the same
   way `docker-compose.yml` already does for `ConnectionStrings__DefaultConnection` - either an
   `appsettings.Production.json` mounted into the container, or the equivalent
   `ConnectionStrings__DefaultConnection`/`Jwt__SecretKey`/etc. environment variables directly).

### Why migrations aren't automatic here

Locally (`dotnet run` or the `api` Docker container), the API applies pending migrations and seeds
default data itself on every startup - convenient for a throwaway dev database, but not something you
want happening automatically against a real, shared production database: multiple instances starting
at once would race to apply the same migration, and an empty production database would silently get
the [documented seeded demo accounts](#seeded-users-all-created-automatically---see-above) inserted
into it. `App:AutoMigrateOnStartup` (`true` in the base `appsettings.json`, `false` in
`appsettings.Production.json.example`) gates both behind that one setting, so treating your production
database's schema as a deliberate, reviewed step (step 2 above) is the default for any environment
that sets it to `false` - not just this specific one.

Anything you don't override in `appsettings.Production.json` still falls back to the base
`appsettings.json` value (e.g. `FileUpload`/`Serilog` settings rarely need a server-specific override) -
you only need to list what's actually different.

`Host=127.0.0.1` in a server-side connection string means "Postgres running on that same server" (as
opposed to `docker-compose.yml`'s `postgres` hostname, which only resolves inside Docker Compose's own
network) - normal for a single-VM deployment where the API and its database share a host.

## Running the test suite

See [CLAUDE.md](CLAUDE.md)'s Commands section for the full list. Short version:

```bash
# Backend (needs Docker running - Integration/Functional tests spin up their own throwaway Postgres
# container via Testcontainers, independent of the docker-compose one above)
dotnet test

# Frontend
cd KhoiProjectManagementApp
npm run test:run

# End-to-end (needs the API + Postgres already running via one of the paths above)
npx playwright test
```
