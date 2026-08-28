---
name: dotnet-api-integration
description: Wire the React frontend to a KhoiProjectManagementApi endpoint, or add/change a backend endpoint an existing frontend feature calls. Use for API client work, DTO shape changes, auth header handling, error response handling, or checking a frontend call against its actual controller.
---

# React <-> .NET 10 API integration (Khoi Pro)

## The real request path

```
React component -> src/services/ApiService.js (the ONLY API client) -> fetch()
    -> Vite dev proxy is NOT used (no proxy in vite.config.js) - VITE_API_URL is called directly
    -> KhoiProjectManagementApi Controller -> ValidationActionFilter (FluentValidation, global)
    -> IXxxService (Application layer) -> IRepository<T>/IUnitOfWork -> Postgres (EF Core)
```

- `VITE_API_URL` is the only frontend env var actually consumed (`ApiService.js`, `wikiHub.js`),
  defaulting to `https://localhost:7148/api` in dev, `/api` (relative) in `.env.production` - the
  relative path works in production because `KhoiProjectManagementApp/vercel.json` rewrites
  `/api/(.*)` to the real backend. If you add a new real-time (SignalR) hub, it needs its own
  `/hubs/(.*)`-style rewrite in `vercel.json` too - a missing one is exactly what silently broke
  Wiki presence in production before the 2026-08-28 audit fixed it.
- JWT is attached fresh per-request as `Authorization: Bearer` (`ApiService.js`'s `authorizedFetch`)
  and refreshed once-and-deduped on a 401 via a shared `_refreshPromise` - don't add a second
  refresh mechanism.

## Backend conventions to follow

- No MediatR/CQRS. A new endpoint's business logic goes in that feature's existing
  `IXxxService`/`XxxService` pair under `KhoiProjectManagement.Application/<Feature>/` - not a new
  handler class.
- Controllers stay thin and inject the service interface only - never a concrete `DbContext`, never
  `IRepository<T>` directly (that's the service's job).
- Add a `CreatedAtAction`/`201` response for anything that creates a persisted entity where a
  matching id-based GET action exists; several existing controllers return `Ok()`/200 or a
  `CreatedAtAction` pointing at a parameterless action instead (a known, not-yet-fixed
  inconsistency - don't copy it into new code).
- `UnauthorizedAccessException` should map to `403 Forbid()` for Space-scoped resources (Vault,
  Wiki, Library) - check how the controller you're extending already does this (a local try/catch;
  there's no shared filter for it yet).
- Every request DTO needs an `AbstractValidator<T>` in that feature's `*Validators.cs` file
  (`Application/<Feature>/<Feature>Validators.cs`) if it has any field worth rejecting bad input
  on - `ValidationActionFilter` picks it up automatically via assembly scan, no controller wiring
  needed.
- **If you're adding a file upload**: use `KhoiProjectManagement.Application.UploadFileNaming.
  BuildStoredFileName(file.FileName)` to build the on-disk filename, never
  `$"{Guid.NewGuid()}_{file.FileName}"` directly - the unsanitized version was a real path-traversal
  vulnerability fixed across 4 services in the 2026-08-28 audit. Read the file back by the stored
  path field, never by the display filename (`AttachmentsController.DownloadFile` had exactly this
  bug - fixed by adding `IAttachmentService.DownloadFileAsync`).

## Response/error handling on the frontend

- `ApiService`'s `buildResponseError()` already parses both FluentValidation's
  `{errors: {field: [msg]}}` shape and a plain `{message}` shape into one `Error` with
  `.status`/`.fieldErrors` - a new call doesn't need its own parsing.
- Distinguish "server said no" (`Error` with `.status`) from "never reached the server"
  (`NetworkError`) from "session expired after refresh failed" (`SessionExpiredError`) by class,
  not by inspecting the message text.

## Verify

After changing both sides of a contract: `dotnet build` (backend), `npm run build` (frontend), and
manually trace the DTO shape both directions - the backend's DTO property names and the frontend's
expected JSON keys must match exactly (no shared type generation exists between them).
