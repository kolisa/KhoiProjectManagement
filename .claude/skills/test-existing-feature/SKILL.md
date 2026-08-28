---
name: test-existing-feature
description: Write or improve tests for existing Khoi Pro functionality (backend service, validator, controller, or frontend component/utility). Use when adding coverage to code that already works, verifying a bug fix, or closing a known coverage gap - not for testing new features.
---

# Testing existing functionality (Khoi Pro)

Test behavior, not implementation details - assert on what a caller/user observes, not on which
private method got called.

## Backend (xUnit)

- **Unit tests** (`tests/KhoiProjectManagement.UnitTests/Services/`): mock `IRepository<T>`/
  `IUnitOfWork` with NSubstitute + MockQueryable (for anything queried via `.Query()`). Follow the
  shape of an existing test in the same area (`VaultServiceTests.cs`, `ProjectServiceTests.cs`,
  etc.) rather than inventing a new mocking pattern. `SpacePermissionResolverTests.cs` is the one
  exception - it uses a real EF Core InMemory-provider `ProjectManagementContext` because the
  permission-inheritance walk is genuinely relational.
- **Functional tests** (`tests/KhoiProjectManagement.FunctionalTests/`): controller-level, via
  `WebApplicationFactory<Program>` + a throwaway Testcontainers Postgres instance - **require
  Docker Desktop running**. Follow `AuthControllerTests.cs`/`ProjectsControllerTests.cs` for the
  fixture setup pattern (`ApiWebApplicationFactory`, `SeededUsers`, `HttpClientAuthExtensions`).
- **Validator tests**: one `*ValidatorsTests.cs` file per feature under
  `tests/KhoiProjectManagement.UnitTests/Validators/` (the original `ValidatorTests.cs` covers
  Auth/Project/Reset-password; `InvoiceValidatorsTests.cs`, `TimesheetValidatorsTests.cs`,
  `IdeaValidatorsTests.cs`, `CalendarValidatorsTests.cs`, `ReminderValidatorsTests.cs`,
  `HrValidatorsTests.cs`, `DashboardWidgetValidatorsTests.cs`, and `WikiValidatorsTests.cs` were
  added 2026-08-28) - follow the per-feature-file convention for a new one rather than growing the
  original file further.

**Feature areas that were zero-coverage before 2026-08-28** (Finance/Invoicing, Timesheets, Ideas,
Calendar, Reminders, HR onboarding, Library, Dashboard/Reports, Wiki's service layer) all got real
unit test suites that day - see `Services/InvoiceServiceTests.cs`, `TimesheetServiceTests.cs`,
`IdeaServiceTests.cs`, `CalendarServiceTests.cs`, `ReminderServiceTests.cs`, `HrServiceTests.cs`,
`LibraryServiceTests.cs`, `DashboardServiceTests.cs`, `DashboardWidgetServiceTests.cs`,
`ReportServiceTests.cs`, `ReportExportServiceTests.cs`, `ReportScheduleServiceTests.cs`, and
`WikiServiceTests.cs`. Backend unit test count went from 123 to 671. Known remaining gaps: the
Quartz job classes themselves (`OverdueTaskCheckJob`, `ReminderDueCheckJob`, etc.) have no test
project of their own - only the services they call do; and several services call `DateTime.UtcNow`/
`DateTime.Now` directly with no injectable clock (`ReminderService`, `DashboardService`,
`InvoiceService`'s `PaidAt` timestamp) - tests work around this with relative/generously-offset
dates rather than asserting exact "now" values; a future `IClock`/`TimeProvider` refactor would
let those be tested precisely. `ReportExportService`'s actual QuestPDF rendering call is untested
(asserting on PDF bytes isn't meaningful; the shared data-shaping logic is tested via the CSV path
instead) since `QuestPDF.Settings.License` is only ever set in the Api project's `Program.cs`, not
in the test project.

## Frontend (Vitest + Testing Library, and Playwright for E2E)

- Unit/component tests are `*.test.js`/`*.test.jsx` colocated with the file they test (`src/utils/`,
  `src/services/`, a few in `src/components/Auth/` and `src/components/Common/`). `App.test.jsx`
  (root-level) covers login/`AuthGuard` flows via MSW-mocked API responses - follow its pattern for
  any test that needs to render past the auth gate.
- **No component test exists for anything under `Wiki/`, `Vault/`, `Library/`, `Reminders/`,
  `Ideas/`, `Finance/`, `Settings/`, `Spaces/`, `Team/`** - real gap, ~8,700 lines of untested
  components as of the 2026-08-28 audit.
- Playwright (`playwright.config.js`, `e2e/login.spec.js`, `e2e/project-crud.spec.js`) is a
  deliberately small, high-value smoke slice against a real dev server + backend - `workers: 1` due
  to observed flakiness against a shared dev DB. Don't assume broad E2E coverage exists; add a new
  spec only for a genuinely critical path, matching the existing two specs' scope, not a full suite.

## CI reality check

`.github/workflows/ci.yml` runs Unit/Architecture tests without Docker on every PR, Integration/
Functional tests with a Postgres service container, and a 25%-line-coverage gate (merged across the
three .NET test projects) that fails the build below it - check `coverlet.runsettings` for what's
excluded (migrations/model-snapshot/`Program` bootstrap) before assuming a low number means missing
tests versus just untested boilerplate.

## Verify

Run the specific test project/file you added coverage to, then the full relevant suite
(`dotnet test` or `npm run test:run`) to confirm you didn't break something else.
