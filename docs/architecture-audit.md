# Architecture Audit — Khoi Pro

Date: 2026-08-28
Scope: security/production-bug-focused pass (Phases 1-2 discovery + baseline, targeted Phase 30
implementation of Critical/High/Medium/Low findings). Full UI modernization (Phases 3-12, 16-17,
25-28) and Claude Skills generation (Phase 33) were **not** attempted in this pass — see
"Deferred to a later phase" at the end.

> **Note on repo state**: while this audit ran, a second, unrelated session/working session was
> also active in this same working directory, adding uncommitted work around email-log status
> tracking, a login-reminder Quartz job, and a queued-email-sending Quartz job (`EmailLogDto.cs`,
> `NotificationService.cs`, `EmailService.cs`, `EmailTemplates.cs`, `AuditController.cs`,
> `ApiService.js`, `AuditLog.jsx`, two new Quartz job files, a new EF Core migration, and new test
> files). None of that is described below — it isn't this audit's work, wasn't reviewed as part of
> it, and this document does not vouch for it. It's called out here only so it isn't mistaken for
> something this pass changed silently. Recommend committing (or reviewing) that work separately
> before it and this audit's changes get tangled into the same commit.

## Executive Summary

The stack is accurately described by this repo's own `CLAUDE.md` **except** for two mismatches in
the audit brief that were corrected before work started: the frontend is plain JavaScript, not
TypeScript (no `tsconfig.json`, no `.ts`/`.tsx` files anywhere), and it targets React 18, not React
19. Vercel deployment is real (confirmed via `vercel.json`).

Baseline (before any changes): backend built clean (0 warnings/errors), 123/123 unit tests passed,
7/7 architecture tests passed, 34/34 functional + 5/5 integration tests failed only because Docker
Desktop isn't running in this environment (Testcontainers-based, not a code defect). Frontend built
clean (with a pre-existing 628KB single-chunk warning), 91/91 tests passed across 14 files.

The audit surfaced one **live production bug** (Wiki real-time presence silently broken through the
Vercel-fronted production path) and one **exploitable security vulnerability** (unsanitized
upload filenames enabling path traversal, repeated in four services) on top of several serious
production configuration issues (wide-open CORS, a shared dev/prod JWT signing key, weak demo
accounts reachable via auto-seeding). All of these have been fixed in this pass; see "Changes
Implemented" below for exactly what changed and how it was verified.

## Findings and Status

| Severity | Area | Finding | Evidence | Recommendation | Status |
|---|---|---|---|---|---|
| Critical | Security | Unsanitized `IFormFile.FileName` used to build on-disk upload paths in 4 services — path traversal / arbitrary file write | `LibraryService.cs:188`, `InvoiceService.cs:211,246,301`, `IdeaService.cs:273`, `AttachmentService.cs:32` (pre-fix line numbers) | Sanitize with `Path.GetFileName()` before using client filename in any path | **Fixed** |
| Critical | Deployment/Security | `Cors:AllowAnyOrigin: true` left enabled in the real production config, with `AllowCredentials()` | `appsettings.Production.json:19` (local, gitignored) | Turn off now that the real origin is known; add scoped preview-origin support instead | **Fixed** |
| Critical | Deployment | Vercel preview deployments have no separate backend/DB — all proxy to production | `vercel.json` (single static file, no per-environment rewrite target) | Provision a separate preview backend, or explicitly restrict/document who can open preview PRs | **Deferred** (infrastructure provisioning, not a code change) |
| Critical | Security | JWT signing key never overridden between dev and prod — anyone with the committed dev key could forge production tokens | `appsettings.json:6` (base, tracked) vs. `appsettings.Production.json` (no prior `Jwt` override) | Give production its own key; rotate | **Fixed** (new key generated for the local prod config — see note below) |
| Critical | Security | Plaintext DB/JWT/SMTP-adjacent credentials committed in the tracked base `appsettings.json` | `appsettings.json:3,6,13,16` | Rotate the real credentials this represents; consider a `.example`-style pattern for the base file too | **Flagged — needs your decision**, not changed (see below) |
| High | Production bug | SignalR Wiki-presence hub (`/hubs/wiki`) has no Vercel rewrite — silently 404s to the SPA fallback in production | `vercel.json` (missing rule), `Program.cs:142` maps the hub, `wikiHub.js:10-13` builds a relative hub URL | Add a `/hubs/(.*)` rewrite | **Fixed** |
| High | Correctness | `AttachmentsController.DownloadFile` built the disk path from the *display* filename instead of the actual GUID-prefixed stored filename — attachment downloads were effectively broken | `AttachmentsController.cs:65` (pre-fix) | Read via the stored path, not the display name (existing pattern already used by Library/Invoice/Idea) | **Fixed** |
| High | Accessibility | None of 19 modal overlays use `role="dialog"`/focus trap/Escape-to-close | grep across `src/components` for `role="dialog"` — 0 matches | Adopt a shared `Modal` wrapper with dialog semantics | **Deferred** to UI modernization phase |
| High | Component architecture | `App.jsx` (2645 lines) redefines `StatusBadge`/`RoleBadge`/`TagsList`/`LoadingSpinner`/`ErrorMessage`/`PriorityBadge`, duplicating and drifting from the `Common/` versions | `App.jsx` (pre-fix lines 70-147) vs. `components/Common/*.jsx` | Dedupe; for the ones with identical behavior, just import | **Partially fixed** — see below |
| Medium | Security | Weak seeded demo accounts (`admin123`/`manager123`/`member123`) reachable on first boot of a real production DB, since `App:AutoMigrateOnStartup` was `true` in the real prod config (against the documented/templated default of `false`) | `appsettings.Production.json` (pre-fix), `DatabaseSeeder.cs:22-79`, `Program.cs:155-160` | Set `AutoMigrateOnStartup` to `false` in production, matching the already-written template and README guidance | **Fixed** |
| Medium | Deployment | Fixed CORS allowlist has no accommodation for Vercel's dynamic preview subdomains | `appsettings.Production.json.example:29-30` | Add a scoped regex option rather than all-or-nothing | **Fixed** |
| Medium | Deployment | Stale/divergent hardcoded CORS fallback origin list in code, including a different (wrong) IP than the one in `vercel.json` | `ServiceCollectionExtensions.cs:180-186` (pre-fix) | Remove the hardcoded fallback | **Fixed** |
| Medium | Deployment | No `UseForwardedHeaders` despite an implied reverse-proxy topology | `Program.cs` (missing) | Add it ahead of `UseHttpsRedirection`/`UseCors` | **Fixed** |
| Medium | Deployment | ~~No reverse proxy~~ — **correction, 2026-08-28 later same day**: a reverse proxy (nginx) was already provisioned on the server (`:80` → `proxy_pass http://127.0.0.1:5000`); the original finding was wrong on that point, confirmed by actually connecting to the server while setting up CI/CD. What's genuinely true: nginx has no TLS certificate configured, so Vercel-to-origin traffic is still plaintext HTTP | `/etc/nginx/sites-enabled/khoi-api` on the server (not visible from the repo, which is why this was missed originally) | `certbot --nginx` once a real hostname exists (see README) | **Partially fixed** — reverse-proxy finding corrected, TLS still outstanding, needs a hostname |
| Medium | EF Core | No index on `User.Email`, `RefreshToken.TokenHash`, `PasswordResetToken.TokenHash` despite exact-match lookups on every login/refresh/reset | `ProjectManagementContext.cs:131-142,205-209` | Add indexes (ideally a case-insensitive one for email) | **Deferred** — needs a migration, left for a dedicated pass |
| Medium | Backend correctness | 3 background/Quartz service methods do a per-item dedup query in a loop instead of one batched query | `NotificationService.cs:139-146,190-196`, `ReminderService.cs:378-385` | Batch the lookup before the loop | **Deferred** |
| Medium | Controllers | `UnauthorizedAccessException` → `403` is duplicated as a try/catch in ~15 controllers instead of one shared exception filter; any controller that forgets it falls through to the middleware's `401` | `ErrorHandlingMiddleware.cs:56-59` vs. per-controller catches | Replace with one `IAsyncExceptionFilter` | **Deferred** |
| Medium | Architecture tests | `LayerDependencyTests` doesn't actually assert "Domain has zero EF Core/package references" or "no MediatR/CQRS anywhere" — both are documentation-only claims today | `tests/.../LayerDependencyTests.cs` | Add the two missing NetArchTest rules | **Deferred** |
| Medium | CI | `ArchitectureTests` project existed but wasn't run in CI | `.github/workflows/ci.yml` (missing step) | Add a step | **Fixed** |
| Low | Deployment | Weak production DB password | `appsettings.Production.json` (local, correctly gitignored) | Rotate | **Flagged — needs your action** (requires live DB access this session doesn't have) |
| Low | Deployment | No backend CD step — green CI doesn't imply production is running that code | `.github/workflows/ci.yml` | Document as manual, or add one | **Documented only** |
| Low | Dead code | `Auth/AuthGuard.jsx` and `Layout/ProjectManagementSystem.jsx` formed a broken import chain (the former imports the latter, which was a literal 0-byte file) on top of already-documented dead code | `Layout/`, `Pages/`, `Models/` folders, `Auth/AuthGuard.jsx`, `Auth/LoginForm.jsx` | Delete (all confirmed zero real importers) | **Fixed** |
| Low | Housekeeping | Orphaned local `KhoiProjectManagement.Models/` directory (build artifacts only, project removed from git in an earlier commit) | Not tracked by git, not in the `.sln` | Delete | **Fixed** |
| Low | Correctness | `WikiService.GetPageSummariesAsync` could throw on a page with null `CurrentContentMarkdown` | `WikiService.cs:108` (pre-fix) | Null-coalesce before `.Split()` | **Fixed** |
| Low | Validation | `CreateScheduledReportDto` and two filter DTOs have no `AbstractValidator<T>` (though the one dangerous field is checked manually in-service) | `ScheduledReportDto.cs:14-18`, `TaskFilterDto.cs`, `ReminderDto.cs:92-104` | Normalize into a validator for consistency | **Deferred** |
| Low | Performance | Frontend ships as one 628KB JS chunk, no code-splitting | `vite.config.js:37` (raised warning limit to accommodate it, rather than splitting) | `React.lazy()` the major feature pages | **Deferred** to a performance pass |

## Changes Implemented

### 1. Path traversal / arbitrary file write (Critical)
**Problem** → Four upload services built the on-disk filename as `$"{Guid.NewGuid()}_{file.FileName}"`
using the client-supplied name verbatim; a GUID prefix doesn't neutralize a `../` sequence later in
the string.
**Change** → Added `KhoiProjectManagement.Application/Common/UploadFileNaming.cs` with a single
`BuildStoredFileName()` helper that sanitizes via `Path.GetFileName()` before the GUID prefix is
applied; updated all four call sites to use it, and sanitized the stored *display* name too so
derived copies (invoice→template, template→invoice) inherit the fix instead of re-introducing it.
**Why** → A single shared helper instead of four separate inline fixes, since it's the same
security-critical logic and needs to stay in sync.
**Files changed** → `KhoiProjectManagement.Application/Common/UploadFileNaming.cs` (new),
`Library/LibraryService.cs`, `Finance/InvoiceService.cs` (3 call sites), `Ideas/IdeaService.cs`,
`Projects/AttachmentService.cs`.
**Verification** → `dotnet build` clean (0 warnings/errors), full `dotnet test` re-run — 123/123
unit tests and 7/7 architecture tests still pass.

### 2. Wide-open production CORS (Critical)
**Problem** → `Cors:AllowAnyOrigin: true` was live in the real production config alongside
`AllowCredentials()` — any website could make credentialed calls to the API. Disabling it outright
would also have broken Vercel preview deployments (dynamic `*.vercel.app` subdomains not in the
fixed allowlist).
**Change** → `ServiceCollectionExtensions.cs`'s CORS policy now uses one `SetIsOriginAllowed`
function that checks the exact `Cors:AllowedOrigins` list, an optional `Cors:PreviewOriginPattern`
regex, and the `AllowAnyOrigin` escape hatch (still available, now off by default) — replacing the
old hardcoded/stale fallback array entirely. Set `Cors:AllowAnyOrigin` to `false` and added a
`PreviewOriginPattern` scoped to this project's own preview subdomains in the local production
config; documented the new option in `appsettings.Production.json.example`.
**Why** → The real production origin is already known and already in `AllowedOrigins`, so the
escape hatch no longer needs to be on; the regex option closes the preview-domain gap that made
`AllowAnyOrigin` look load-bearing.
**Files changed** → `KhoiProjectManagementApi/Extensions/ServiceCollectionExtensions.cs`,
`appsettings.Production.json` (local, gitignored), `appsettings.Production.json.example`.
**Verification** → `dotnet build` clean; unit tests unaffected (no test exercised this policy
directly). **Needs deploying** to the real server to take effect — this only changed the local file.

### 3. Shared dev/prod JWT signing key (Critical)
**Problem** → Production never overrode `Jwt:SecretKey`, so it signed/validated tokens with the
same key committed in the tracked `appsettings.json`.
**Change** → Generated a new 512-bit random key and added a `Jwt:SecretKey` override to the local
`appsettings.Production.json`.
**Why** → Anyone with the (committed, git-history-exposed) dev key could otherwise forge valid
production tokens.
**Files changed** → `appsettings.Production.json` (local, gitignored).
**Verification** → N/A beyond a config change — **this needs deploying**, and will invalidate every
currently-active production session (all users will need to log in again) the moment it's deployed.
Flagging this explicitly since it's an operational consequence, not a bug.

### 4. Weak seeded demo accounts reachable in production (Medium)
**Problem** → `App:AutoMigrateOnStartup` was `true` in the real production config (the tracked
`.example` template already documents `false` as the correct production value, with an explanation
of exactly this risk — the real file just wasn't following it), so a first boot against an empty
production database would auto-seed `admin123`/`manager123`/`member123` accounts.
**Change** → Set `App:AutoMigrateOnStartup` to `false` in the local production config, matching the
already-documented/templated intent.
**Files changed** → `appsettings.Production.json` (local, gitignored).
**Verification** → No code change; **needs deploying**. After this deploys, schema changes must be
applied via the already-documented `dotnet ef database update` step (README "Deploying to a
server") rather than happening automatically on startup.

### 5. SignalR Wiki hub unreachable in production (High — live bug)
**Problem** → `vercel.json` only rewrote `/api/*`; `/hubs/wiki` fell through to the SPA catch-all
and got served `index.html`, silently breaking Wiki co-presence/edit-locking in production.
**Change** → Added a `{ "source": "/hubs/(.*)", "destination": "http://160.119.249.227/hubs/$1" }`
rewrite rule.
**Files changed** → `KhoiProjectManagementApp/vercel.json`.
**Verification** → Config-only change; **needs a Vercel redeploy** to take effect, and needs
verifying live afterward that WebSocket upgrade actually proxies through Vercel's edge (SignalR
falls back to long-polling if not, so functionality should recover either way, but worth a manual
check).

### 6. Broken attachment downloads (High — correctness bug)
**Problem** → `AttachmentsController.DownloadFile` read the file from disk using the attachment's
*display* filename, but the file was saved under a GUID-prefixed name — so downloads almost always
404'd, unlike the equivalent Library/Invoice/Idea features which already read by stored path.
**Change** → Added `IAttachmentService.DownloadFileAsync(id)` (matching the exact
`Task<(byte[] Content, string ContentType, string FileName)?>` pattern already used by
`InvoiceService`/`LibraryService`), reading via the correct `FilePath` field; the controller now
calls this instead of touching the filesystem itself.
**Files changed** → `KhoiProjectManagement.Application/Projects/IAttachmentService.cs`,
`Projects/AttachmentService.cs`, `KhoiProjectManagementApi/Controllers/AttachmentsController.cs`.
**Verification** → `dotnet build` clean; `dotnet test` — 123/123 unit tests pass.

### 7. Missing forwarded-headers handling (Medium)
**Change** → Added `ForwardedHeadersOptions` configuration (X-Forwarded-For/Proto,
`KnownIPNetworks`/`KnownProxies` cleared since the eventual reverse proxy's address isn't fixed
here) and `app.UseForwardedHeaders()` ahead of `UseHttpsRedirection`/`UseCors` in `Program.cs`.
**Files changed** → `KhoiProjectManagementApi/Program.cs`.
**Verification** → `dotnet build` clean (had to switch from the obsolete `KnownNetworks` to
`KnownIPNetworks` after a first-pass build warning — fixed before finalizing).

### 8. No TLS / reverse proxy documented (Medium) — finding corrected, TLS itself still open
**Original change** → Added a "Reverse proxy and TLS" section to `README.md`, recommending
provisioning a reverse proxy (Caddy) since none was believed to exist.
**Correction (same day, later pass)** → While setting up CI/CD deploy access, actually connected to
the production server and found nginx already running as a reverse proxy
(`/etc/nginx/sites-enabled/khoi-api`, `:80` → `proxy_pass http://127.0.0.1:5000`, already sending
`X-Forwarded-Proto`/`X-Forwarded-For`) — the original "no reverse proxy" finding was wrong; this repo
has no visibility into server-only config files, which is exactly why it was missed. Updated
`README.md`'s section to describe the real nginx setup and the `certbot --nginx` path (simpler than
standing up a new Caddy instance, since nginx is already there) instead of recommending a new proxy.
**Still genuinely missing** → nginx has no TLS certificate — Vercel-to-origin traffic is still
plaintext HTTP. Needs a real hostname pointed at the server before a certificate can be issued;
tracked as the one item this pass still couldn't close.

### 9. Architecture tests not run in CI (Medium)
**Change** → Added an `Architecture tests` step to the `backend` job in `.github/workflows/ci.yml`,
ahead of the Docker-dependent Integration/Functional steps (NetArchTest is static analysis, no
Docker needed).
**Files changed** → `.github/workflows/ci.yml`.
**Verification** → Ran `dotnet test tests/KhoiProjectManagement.ArchitectureTests` locally — 7/7 pass.

### 10. Dead/broken frontend code (Low)
**Problem** → `src/components/Layout/`, `Pages/`, `Models/`, and `Auth/AuthGuard.jsx` +
`Auth/LoginForm.jsx` had zero real importers anywhere in the app (confirmed via grep before
deleting) — worse than previously documented, since `Layout/ProjectManagementSystem.jsx`,
`Pages/Team.jsx`, `Models/TaskModal.jsx`, and `Models/TeamMemberModal.jsx` were literal 0-byte
files, meaning `Auth/AuthGuard.jsx`'s import of `ProjectManagementSystem` couldn't have worked even
if something had reached it.
**Change** → Deleted all of it (11 files across 3 folders).
**Files changed** → removed `Auth/AuthGuard.jsx`, `Auth/LoginForm.jsx`, `Layout/*` (3 files),
`Pages/*` (5 files), `Models/*` (3 files).
**Verification** → Confirmed zero importers via `grep` across `src/` before deleting; `npm run
build` succeeded afterward (1906 modules transformed, same as before); `npm run test:run` — 91/91
tests still pass across all 14 files.

### 11. Duplicated/drifted shared components in App.jsx (High, partial)
**Problem** → `App.jsx` locally redefined `StatusBadge`, `PriorityBadge`, `RoleBadge`, `TagsList`,
`LoadingSpinner`, and `ErrorMessage`, duplicating (and in three cases, having drifted from) the
`components/Common/` versions already used by the newer feature modules.
**Change** → `LoadingSpinner` and `ErrorMessage` were byte-for-byte identical between the two
copies, and `PriorityBadge` was logic-identical — all three now import from `Common/` instead of
redefining locally. `StatusBadge`, `RoleBadge`, and `TagsList` were **left as-is**: their `Common/`
counterparts have genuinely different colors/icons/text-formatting (e.g. `Common/StatusBadge`
doesn't replicate App.jsx's `.replace('-', ' ')` display transform, and `Common/RoleBadge` adds
icons and a different color palette App.jsx's version doesn't have) — swapping those would be a
visible UI change, not a safe dedup, and this pass was scoped to preserve existing behavior exactly.
**Files changed** → `KhoiProjectManagementApp/src/App.jsx`.
**Verification** → `npm run build` and `npm run test:run` both clean, 91/91 tests passing.
**Recommendation for later** → When UI modernization is scheduled, make a deliberate choice between
the two visual styles for Status/Role badges and Tags, then finish the dedup.

### 12. Orphaned local build-artifact directory (Low)
**Change** → Deleted `KhoiProjectManagement.Models/` — confirmed via `git status`/`git log` to be
untracked, not referenced by the `.sln`, and not referenced by any `.csproj` (the actual project
was removed from git in an earlier commit; only stale `obj/` cache files were left on disk).
**Verification** → `dotnet build` clean afterward (no project referenced it).

### 13. Minor null-reference risk (Low)
**Change** → `WikiService.GetPageSummariesAsync` null-coalesced `page.CurrentContentMarkdown`
before `.Split()` — a nullable-reference warning that surfaced during an incremental rebuild and
represented a real (if narrow) crash risk for any wiki page with null content.
**Files changed** → `KhoiProjectManagement.Application/Wiki/WikiService.cs`.

## Flagged — needs a decision only you can make, not changed

- **Plaintext credentials committed in the tracked base `appsettings.json`** (DB password, JWT dev
  key, a Gmail account + what appears to be a real (non-App-Password-shaped) password for
  `kolisa.biza@gmail.com`). Per this repo's own `CLAUDE.md`, the standing guidance is to flag this
  rather than propagate it further — editing the file wouldn't remove it from git history anyway.
  Recommend: rotate the Postgres password and the Gmail credential (and switch to a proper Gmail
  App Password if not already), and decide separately whether git history needs scrubbing given
  this has been exposed since whenever these commits landed.
- **Weak production DB password** — still not rotated. Server SSH access now exists (see the CI/CD
  section below), which removes the earlier practical blocker, but rotating it also means updating
  the Postgres role's password to match, and this session wasn't asked to do that — flagging rather
  than doing it unprompted, since a mismatched rotation would take the API down.
- **Vercel preview deployments sharing the production database** — needs either a separate
  preview backend/DB or a documented policy about who can open preview PRs; both are infrastructure
  decisions, not code changes.
- **TLS on the production server** — the reverse-proxy half of this finding was wrong (see the
  correction under #8 below: nginx already exists); TLS itself is still missing and needs a real
  hostname pointed at `160.119.249.227` before a certificate can be issued — a domain/DNS decision
  only you can make.

## Deferred to a later phase

The full 35-phase audit-and-modernize engagement this was scoped from includes UI modernization,
backend/frontend test coverage, and Claude Skills generation. All of these — plus the two bugs
discovered incidentally while writing frontend tests, and a performance pass — have since been
completed in follow-up work the same day:

- **Backend test coverage**: Finance/Timesheets/Ideas/Calendar/Reminders/HR/Library/Dashboard/
  Reports/Wiki-service (all zero-coverage as of this audit) now have real unit test suites —
  backend unit tests went from 123 to 671, all passing. See `.claude/skills/test-existing-feature`
  for the current state and known remaining gaps (Quartz job classes themselves, a few
  `DateTime.UtcNow`-dependent behaviors, QuestPDF's actual rendering call).
- **Frontend test coverage**: Wiki/Vault/Library/Reminders/Ideas/Finance/Settings/Team/Spaces
  component tests added — frontend tests went from 91 to 200, all passing. Every subagent-written
  test was independently verified by actually running it, not trusted on report alone; ~10 of the
  new tests had real bugs on first run (ambiguous DOM-text queries matching a stray `<option>` or
  duplicate badge, stale node references reused inside `waitFor`, one genuine race in an
  optimistic-update assertion, and one confirmed jsdom/Node cross-realm bug where a `File`-bodied
  `fetch()` request never resolves on the receiving end) — all fixed, none swept under a `skip`.
- **Two bugs found while writing those tests, now fixed**: `Common/RoleBadge.jsx` crashed on any
  role value outside `admin`/`manager`/`member` (renders `<undefined />`); `IdeasPage.jsx` stayed on
  its "Loading..." placeholder forever after a failed load, underneath the error message that had
  already rendered, because the error path never set `ideas` to a non-null value.
- **Claude Code skills (Phase 33-34)**: the 9 skills were generated and the 4 wrong-stack existing
  skills (`postgres-dapper`, `dotnet-clean-architecture`, `fullstack-feature-builder`,
  `react-premium-ui`) removed. See `docs/claude-skills.md`.
- **UI modernization**: all 18 modal instances across 12 files now wire `role="dialog"`/Escape-to-
  close/focus-trap via a new shared `useModalA11y` hook (`components/Common/useModalA11y.js`) — the
  hook itself needed a mid-flight fix from a plain `useRef`+effect to a callback ref, since the
  former never actually attached when a modal was inline conditional JSX inside an always-mounted
  parent (exactly `App.jsx`'s own 4 modals) rather than its own mount/unmount component; caught by
  one of the four agents doing the mechanical per-file rollout, and it retroactively fixed the
  bug in the 4 modals already wired with the old version. `App.jsx`'s own `StatusBadge`/`RoleBadge`/
  `TagsList` are now deduped onto the `Common/` versions (a deliberate design choice: `Common/`'s
  style is canonical, including `RoleBadge`'s icons/palette, which is a visible change from
  `App.jsx`'s old plain version). `Settings/AuditLog.jsx`'s table now scrolls instead of clipping
  content on narrow viewports. `App.jsx`'s `LoginForm` was extracted to `Auth/LoginForm.jsx` (2,610
  → 2,434 lines) — a fresh live file at the same path a same-day-earlier pass had deleted as dead
  code, not a resurrection of that old file.
- **Performance**: the six heavier feature-tab pages and five Settings panels are now
  `React.lazy()`-loaded from `App.jsx` instead of eagerly bundled — main JS chunk dropped from
  ~628KB to ~280KB, each feature page its own chunk loaded on first visit to that tab.

## CI/CD deploy automation (separate follow-up pass, same day)

The manual deploy process described in the (now largely superseded) README section was replaced
with a `deploy` job in `.github/workflows/ci.yml` — runs after `backend`/`frontend`/`e2e` all pass
on a push to `master`, does exactly what the manual steps did (stop `khoi-api`, clear
`/var/www/khoi-api`, publish + upload the new build, fix ownership, `daemon-reload` + `restart`,
verify Swagger responds), authenticated with a dedicated SSH key generated specifically for this
(not the user's own broken RSA key) whose public half was added to the server's `authorized_keys`,
private half stored as the `DEPLOY_SSH_KEY` repository secret. See README's "CI/CD deploy" section.

**Setting this up surfaced findings the original audit couldn't have caught** (nothing in the repo
reveals server-only state) — corrected here rather than left stale:

- **The "no reverse proxy" finding (#8 above) was wrong.** nginx is already running on the server,
  proxying `:80` to Kestrel on `127.0.0.1:5000` and already sending `X-Forwarded-*` headers. Only the
  TLS half of that finding was actually true.
- **The Critical JWT-secret and CORS `AllowAnyOrigin` fixes from earlier the same day were never
  actually live in production.** Both were fixed in this git checkout's local `appsettings.
  Production.json` — but the real server doesn't deploy that file at all; its manual process instead
  shipped a *separately, manually maintained* `appsettings.Production.json` living outside git in the
  operator's local publish folder, which had neither fix (no `Jwt` section at all, `Cors:
  AllowAnyOrigin` still `true`). Confirmed by reading the actually-deployed file on the server.
  **Now fixed for real**: while wiring up the deploy key, also migrated every value out of that
  server-local file into `/etc/khoi-api/khoi-api.env` as environment variables (the mechanism the CI
  job's publish output relies on, since it correctly never carries a `Production.json`), and set
  `Cors__AllowAnyOrigin=false` (was `true`) plus `Jwt__SecretKey` (was absent, meaning the shared dev
  key from the committed base `appsettings.json` was signing production tokens) while doing so.
  Deploying the new JWT key invalidated every active session — expected, not a bug.
- **`App:AutoMigrateOnStartup` is `true` on the real server**, not `false` as this repo's own
  documented default recommends — confirmed to be a deliberate choice made when CI/CD was set up
  (single-instance deployment, already-populated database, explicit preference for automatic
  migrations over a manual per-release step), not something this pass overrode unprompted. See
  README's "Why migrations usually aren't automatic here" for the full reasoning.

## Still deferred

`App.jsx`'s `AuthGuard` and the ~2,300-line `ProjectManagementSystem` component (the Dashboard/
Projects/Tasks/Team tab bodies, their state, and their remaining CRUD modals) were deliberately
**not** further decomposed — `AuthGuard` can't be extracted without a circular import back into
`App.jsx` (it directly renders `ProjectManagementSystem`), and splitting the tab bodies out would
mean threading ~15 interdependent state variables and handler functions across new component
boundaries in the app's single most-used code path — a meaningfully higher-risk change than
anything else in this pass, better suited to its own dedicated `regression-safe-refactor` session
with room to verify each extraction in isolation, rather than being rushed alongside everything
else here. Frontend component/unit test coverage for the newer feature areas exists now (see
above), but nothing tests `App.jsx`'s own `ProjectManagementSystem`/`AuthGuard` bodies directly
beyond what `App.test.jsx` already covered (login flow, Projects tab, project edit) — a natural
next target once/if that component gets decomposed enough to test in smaller pieces.

## Out-of-Scope New Features

None identified. Every finding in this pass was about existing, already-shipped functionality
(a broken download, a broken real-time feature, insecure defaults, dead code) — nothing here
proposes new business functionality.
