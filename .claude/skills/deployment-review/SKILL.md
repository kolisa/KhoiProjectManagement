---
name: deployment-review
description: Review a deployment-related change to Khoi Pro before it ships - Vercel config, appsettings.Production.json, CORS, CI workflow, or the Linux backend's reverse-proxy setup. Use as a pre-ship checklist; use deployment-debugging instead if something is already broken in a live deployment and needs diagnosing.
---

# Deployment review (Khoi Pro: Vercel frontend + Linux/.NET backend)

Complements `deployment-debugging` (that skill diagnoses an already-broken deployment; this one
reviews a change *before* it ships). Don't deploy anything yourself unless explicitly asked -
review and report.

## The real topology

```
Vercel (React/Vite, static build in build/)
   |  vercel.json rewrites /api/(.*) and /hubs/(.*) to the backend
   v
Linux server, Kestrel on :8080 (Dockerfile: ASPNETCORE_URLS=http://+:8080)
   |  NOTE: as of 2026-08-28 there is no reverse proxy or TLS termination documented/configured -
   |  UseHttpsRedirection()/UseForwardedHeaders() are no-ops without one. See README.md's
   |  "Reverse proxy and TLS" section for the required Caddy/nginx setup - if you're reviewing a
   |  deployment change, check whether this has been addressed yet before assuming HTTPS end-to-end.
   v
Postgres (same host, Host=127.0.0.1 in the connection string)
```

## Checklist for any change touching deployment

- **`vercel.json` rewrites**: does every backend entry point the frontend calls have a matching
  rewrite (`/api/(.*)` and `/hubs/(.*)` today)? A new SignalR hub or a new top-level route family
  needs its own rule - a missing one fails silently (falls through to the SPA catch-all, returns
  `index.html` instead of an error), exactly what broke Wiki presence before the 2026-08-28 fix.
- **CORS** (`appsettings.Production.json`, real file is gitignored - check
  `appsettings.Production.json.example` for the documented shape): `Cors:AllowAnyOrigin` must be
  `false` in any real deployment - it's a temporary escape hatch, not a setting to leave on.
  `Cors:AllowedOrigins` needs the exact production origin; `Cors:PreviewOriginPattern` (added
  2026-08-28) is the mechanism for Vercel's dynamic preview subdomains - don't reach for
  `AllowAnyOrigin` to solve a preview-CORS problem.
- **`App:AutoMigrateOnStartup`**: must be `false` for any real deployment
  (`appsettings.Production.json.example` documents why - concurrent-instance migration races and
  auto-seeding demo accounts against a real DB). If `true` shows up in a production config review,
  that's a Medium-or-higher finding, not a style nit.
- **`Jwt:SecretKey`**: production must override the base `appsettings.json`'s value with its own.
  Deploying a new key invalidates every active session - call this out explicitly, it's an
  operational consequence someone needs to know about ahead of time, not a silent side effect.
- **CI** (`.github/workflows/ci.yml`): `backend`/`frontend` jobs run on every PR + push to
  `master`; the heavier `e2e` job (real Postgres + API + Playwright) only runs on push/manual
  dispatch by design (keeps PR CI fast) - don't "fix" that gating without understanding why it's
  there. There is currently no automated backend *deploy* step - green CI doesn't mean production
  is running that code; deployment is a manual `dotnet ef database update` + process restart
  (documented in README's "Deploying to a server").

## Secrets

Never print the contents of `appsettings.Production.json` (gitignored, contains real credentials)
in full back to a user or into a committed file. The base `appsettings.json` is tracked and
contains real-looking dev credentials by design (per `CLAUDE.md`) - don't add more secrets there;
flag rather than propagate.

## Output

State what was actually checked and its current value/status - not just "CORS looks fine", but
"`Cors:AllowAnyOrigin` is `false`, `AllowedOrigins` contains the production Vercel domain, verified
against `ServiceCollectionExtensions.cs`'s policy logic."
