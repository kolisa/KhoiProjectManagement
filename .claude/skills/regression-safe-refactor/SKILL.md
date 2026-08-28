---
name: regression-safe-refactor
description: Refactor, clean up, or restructure existing Khoi Pro code (backend or frontend) while proving behavior didn't change. Use when simplifying, deduplicating, extracting, renaming, or reorganizing working code - not for new functionality.
---

# Regression-safe refactor (Khoi Pro)

## Workflow

1. **Inspect** the code as it stands - read the whole file/component being changed, not just the
   section that looks wrong.
2. **Understand** why it's shaped the way it is before changing it. This codebase has real
   intentional-but-non-obvious design (documented in `CLAUDE.md`'s Architecture Notes) - e.g.
   Quartz triggers are deliberately started an hour out to avoid a misfire-recovery double-fire
   race, `AuthService.RequestPasswordResetAsync` deliberately swallows a failed email send to avoid
   an enumeration side-channel, `LibraryService`/`InvoiceService`/`IdeaService` deliberately read
   files back by stored path not display name. Don't "simplify" away something that's actually a
   fix for a bug that isn't visible from the diff alone.
3. **Identify callers** - `grep` for every usage of what you're changing before touching it,
   including string-based references (route paths, config keys, `nameof()` targets) that a
   type-level search would miss.
4. **Identify tests** - find existing tests covering the code (`tests/KhoiProjectManagement.
   UnitTests/Services/`, `*.test.js` colocated with frontend utils) before changing behavior they
   assert on.
5. **Establish a baseline** - run the relevant tests/build *before* changing anything, so a later
   failure is attributable to your change, not a pre-existing issue.
6. **Refactor.**
7. **Test** - re-run the same tests from step 5.
8. **Build** - `dotnet build` and/or `npm run build`, whichever side you touched.
9. **Review the diff** - confirm nothing changed beyond the intended refactor (no accidental
   formatting-only churn across unrelated lines, no behavior change smuggled in).

## Known dead code (safe to touch/remove without further investigation)

`src/components/Layout/`, `Pages/`, `Models/` were confirmed zero-importer dead code and removed in
the 2026-08-28 audit. `Auth/LoginForm.jsx` at that same path was ALSO confirmed dead and deleted that
day, then re-created later the same session as a genuinely live file (extracted out of `App.jsx`) -
don't assume a file at a "known dead" path is still dead without checking; `grep` for real importers
first, since new dead code (or a live file replacing an old dead one) can both happen after that pass.

## What "prove behavior is intact" means here

- **Backend**: `dotnet build` clean, then `dotnet test tests/KhoiProjectManagement.UnitTests` and
  `tests/KhoiProjectManagement.ArchitectureTests` (both run without Docker).
  `IntegrationTests`/`FunctionalTests` need Docker Desktop running (Testcontainers-based) - if it's
  unavailable in your environment, say so explicitly rather than reporting them as passed.
- **Frontend**: `npm run build` and `npm run test:run` both clean. There's no `npm run lint`.
- If the refactor touches a shared component (anything in `Common/`) or a cross-feature service
  (`Application/Common/`, `Abstractions/`), check every consumer, not just the one you started from
  - `IRepository<T>`/`IUnitOfWork` and the `Common/*Badge.jsx` components in particular have many
  callers.
- A refactor that changes visible text, colors, or layout is a UI change, not a pure refactor - flag
  it rather than bundling it in silently (see `ui-modernization` for that work instead).

## Don't

- Don't fix unrelated things you notice mid-refactor - note them for a separate pass instead
  (`docs/architecture-audit.md` has a running list of known, deferred issues to add to).
- Don't use this skill to change business behavior "while you're in there" - a genuine bug fix is
  fine and worth calling out explicitly as a fix, but silent behavior drift disguised as cleanup
  is exactly what this skill exists to prevent.
