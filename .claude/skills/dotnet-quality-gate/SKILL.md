---
name: dotnet-quality-gate
description: Run the KhoiProjectManagement .NET solution's quality checks (restore, build, tests) and report actual results. Use before considering backend work done, or when asked to verify the backend is in a good state.
---

# .NET quality gate (Khoi Pro)

Run from the repo root (`KhoiProjectManagement.sln` covers all 5 source projects + 4 test
projects).

```bash
dotnet restore
dotnet build --no-restore
```

Then tests, in order of what actually needs Docker:

```bash
# No Docker required:
dotnet test tests/KhoiProjectManagement.UnitTests
dotnet test tests/KhoiProjectManagement.ArchitectureTests

# Require Docker Desktop running (Testcontainers spins up a throwaway Postgres 16 container per
# project) - if Docker isn't available in your environment, say so explicitly rather than silently
# skipping or reporting these as passed:
dotnet test tests/KhoiProjectManagement.IntegrationTests
dotnet test tests/KhoiProjectManagement.FunctionalTests
```

Or all four in one call (`dotnet test` from the root) - fine when Docker is available; when it
isn't, running the two Docker-independent projects individually gives a cleaner signal than one
combined run with two projects failing for an environment reason.

## What "green" looks like right now (baseline as of 2026-08-28, end of day)

- `dotnet build`: 0 warnings, 0 errors.
- `UnitTests`: 671/671 passing (was 123 earlier the same day, before a pass closed the
  Finance/Timesheets/Ideas/Calendar/Reminders/HR/Library/Dashboard/Reports/Wiki-service coverage
  gaps - see `test-existing-feature`).
- `ArchitectureTests`: 7/7 passing (now also run in CI's `backend` job, ahead of the Docker-
  dependent steps - added in the 2026-08-28 audit; it wasn't wired into CI before that despite the
  project existing).
- `IntegrationTests`/`FunctionalTests`: need a real Postgres reachable via Testcontainers - fail
  with `DockerUnavailableException` if Docker isn't running, which is an environment limitation,
  not a code defect. CI runs these on `ubuntu-latest`, which has Docker preinstalled.
- CI additionally enforces a 25% merged-line-coverage floor (`coverlet.runsettings` excludes
  generated migrations/model-snapshot/`Program` bootstrap from the count) - not something to run
  locally by default, but worth knowing a coverage-shrinking change could fail CI even if all tests
  pass.

## Report format

State the actual command run and actual result - pass/fail counts per project, warning text if any.
**Never report a check as passed without having run it**, and never report Integration/Functional
tests as passing if Docker wasn't actually available to run them against.

## Stop and report failures

If `dotnet build` fails or a test project regresses, stop and report the actual error with the
project/test name - don't silently exclude a failing project from the report.
