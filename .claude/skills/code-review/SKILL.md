---
name: code-review
description: Review a Khoi Pro change against this repo's specific architecture rules and known gotchas before it ships - layering violations, permission-check mistakes, upload-path handling, dead-code traps. Use alongside (not instead of) a general correctness/simplification review; this one only knows what's specific to this codebase.
---

# Khoi Pro code review checklist

This is a **repo-specific supplement**, not a substitute for a general correctness/simplification/
security review - it catches the mistakes that are specific to this codebase's conventions, which a
generic review has no way to know.

## Architecture boundaries (enforced by `tests/KhoiProjectManagement.ArchitectureTests`, but not
## everything is - check these by hand too)

- `KhoiProjectManagement.Domain` must stay pure POCOs - no EF Core, no package references, no
  `DataAnnotations`. **Not currently enforced by a test** (NetArchTest only checks namespace
  dependencies, not `.csproj` package references) - watch for this by hand.
- No MediatR/CQRS anywhere - also **not currently test-enforced**. If a diff introduces an
  `IRequest`/`IRequestHandler`/`*Handler` class, that's a real architecture violation for this repo.
- Services inject `IRepository<T>`/`IUnitOfWork`, never a concrete `ProjectManagementContext` -
  except `SpacePermissionResolver` (Infrastructure, legitimately concrete) and the one narrow
  `IWikiSearchRepository` full-text-search exception.
- Controllers only in `KhoiProjectManagementApi/Controllers/`, `DbContext` subclasses only in
  `Infrastructure/Data/`, `*Repository` classes only in `Infrastructure/Repositories/` - these
  three **are** enforced by `LayerDependencyTests.cs`.

## Correctness patterns specific to this repo

- **File uploads**: the stored on-disk filename must go through
  `KhoiProjectManagement.Application.UploadFileNaming.BuildStoredFileName()` (sanitizes via
  `Path.GetFileName()`) - a raw `$"{Guid.NewGuid()}_{file.FileName}"` is a path-traversal bug (this
  exact pattern was found and fixed in 4 services in the 2026-08-28 audit). Reads must go by the
  stored path field, never the display filename.
- **Space-scoped resources** (Vault, Wiki, Library): a denied access should map to `403`
  (`UnauthorizedAccessException` caught and converted) not fall through to the middleware's default
  `401`.
- **Quartz triggers**: a new recurring trigger's `.WithSimpleSchedule(...)` start time must not
  default to "now" (or a short fixed offset) - use `DateBuilder.FutureDate(...)` like the existing
  triggers in `Program.cs`, or a misfire-recovery race can double-fire it.
- **CORS**: `Cors:AllowAnyOrigin` must never be `true` in a real deployed config - it's a documented
  temporary escape hatch only.

## Frontend traps

- `src/components/Layout/`, `Pages/`, `Models/` don't exist anymore (deleted as confirmed dead code)
  - if a diff references them, that's either resurrecting dead code or a sign the diff is stale/based
  on an old branch. `Auth/AuthGuard.jsx` is still inline in `App.jsx` (extracting it would create a
  circular import back into `App.jsx`), but `Auth/LoginForm.jsx` was extracted out and is genuinely
  live again as of 2026-08-28 - a diff touching it is not automatically suspect.
- A new component redefining `StatusBadge`/`PriorityBadge`/`RoleBadge`/`LoadingSpinner`/
  `ErrorMessage`/`TagsList` locally instead of importing from `components/Common/` is repeating a
  mistake already found and fixed in `App.jsx` - all six are deduped onto the `Common/` versions.
- A modal missing `role="dialog"`/Escape-to-close/focus-trap is a regression - every modal in the app
  now wires this via the shared `useModalA11y` hook (`components/Common/useModalA11y.js`); a new
  modal that skips it, or reimplements the same behavior by hand, is a code-review finding.
- Direct `fetch()` calls from a component instead of going through `ApiService.js` bypass its auth
  header injection, refresh handling, and error normalization.

## Validation

Every new request DTO should have a matching `AbstractValidator<T>` in that feature's
`*Validators.cs` unless there's a specific reason not to (e.g. the field is checked manually
in-service for a small enum-like set - see `ReportScheduleService.CreateScheduleAsync` for a
documented instance of that exception, not a pattern to copy without reason).

## Output

Findings by severity, each with file:line, what's wrong, and why it matters *in this codebase*
specifically - not generic advice a linter would already catch.
