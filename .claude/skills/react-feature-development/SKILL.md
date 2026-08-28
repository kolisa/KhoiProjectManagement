---
name: react-feature-development
description: Build or extend a feature in the KhoiProjectManagementApp React frontend - new component, page section, form, or modal for an existing feature area (Projects, Tasks, Vault, Wiki, Library, Ideas, Finance, Reminders, Settings, Team). Use when adding UI for functionality the backend already supports, not for inventing new business features.
---

# React feature development (Khoi Pro frontend)

Stack: Vite + React 18 + **plain JavaScript** (never TypeScript - no `tsconfig.json`, no `.ts`/`.tsx`
files exist in this repo) + Tailwind CSS. No router library, no TanStack Query, no Redux/Zustand, no
Zod. Files with JSX use `.jsx`; plain-logic files (`services/`, `utils/`) use `.js`.

## Where things go

- One folder per feature under `src/components/` (`Vault/`, `Wiki/`, `Library/`, `Ideas/`,
  `Finance/`, `Reminders/`, `Settings/`, `Team/`, `Spaces/`) - a feature's page, list, detail,
  modal, and editor components all live together in that folder, not split into project-wide
  `pages/`/`modals/`/`forms/` folders.
- `src/components/Common/` holds genuinely shared primitives already in use:
  `StatusBadge`/`PriorityBadge`/`RoleBadge`/`TagsList`/`LoadingSpinner`/`ErrorMessage`/`Toggle`/
  `RandIcon`/`ShareButton`/`OfflineBanner`/`UpdateAvailableBanner`. Reuse these; don't redefine a
  local copy in a new component the way `App.jsx` used to (fixed in the 2026-08-28 audit - see
  `docs/architecture-audit.md`).
- `src/services/ApiService.js` is the **single** API client (a class, sectioned by feature with
  comments) - add new calls there, never `fetch()` directly from a component.
- `src/contexts/AuthContext.jsx` (current user) and `ToastContext.jsx` (toast queue) are the only
  two contexts - don't add a new context for state a `useState` in the feature's page component
  can hold instead.
- `src/utils/` for pure helper functions, colocated with a `*.test.js` file.

## Dead folders - do not add files here

`Layout/`, `Pages/`, and `Models/` were all confirmed to have zero real importers and deleted in the
2026-08-28 audit. If you're tempted to add a new page under `Pages/`, stop - check where the
equivalent existing tab is actually rendered in `App.jsx` first (`ProjectManagementSystem` and
`AuthGuard` are still defined inline there - extracting `AuthGuard` specifically would create a
circular import, since it directly renders `ProjectManagementSystem`). `LoginForm` was extracted to
`Auth/LoginForm.jsx` in that same pass (a fresh live file, not a resurrection of the old dead one at
that path) - along with `Auth/ForgotPasswordForm.jsx` and `Auth/ResetPasswordForm.jsx`, all three are
genuinely live.

## Conventions

- Server data (projects, tasks, wiki pages, ...) lives in local `useState` inside the feature's
  page component, with a `loading`/`error` pair alongside it - not a global store. Look at
  `Wiki/WikiPage.jsx` or `Reminders/RemindersPage.jsx` for the established `loading`/`error`/`data`
  triad before inventing a new pattern.
- Errors from `ApiService` surface via `reportApiError(toast, error, fallback)`
  (`utils/apiError.js`) + `toast.error()` from `ToastContext` - use this instead of a bespoke error
  state where a toast is the right UX.
- `ApiService` throws `NetworkError` (offline/timeout/CORS) and `SessionExpiredError` (401 after a
  failed refresh) as distinct classes from a normal `Error` with `.status`/`.fieldErrors` - if a
  component needs to distinguish "server rejected this" from "never reached the server", check the
  error's type/class, don't parse a message string.
- No formal `Button`/`Input`/`Modal` component library exists yet - most forms/modals hand-roll
  Tailwind classes. Match the existing visual style of the feature folder you're working in rather
  than inventing new spacing/radius/shadow values. If you're changing shared visual patterns across
  many files, that's `ui-modernization` territory, not this skill.
- Every `IFormFile`-adjacent upload flows through `ApiService`'s multipart methods - check
  `ApiService.js`'s Vault/Library/Ideas/Finance sections for the existing pattern before adding a
  new one.

## Before writing code

1. Confirm the backend already supports what you're building (check the matching
   `KhoiProjectManagement.Application/<Feature>/` service + its controller) - this skill is for
   frontend work on existing backend functionality, not inventing new endpoints.
2. Grep for an existing similar component in the same feature folder to match its structure.
3. After writing: `cd KhoiProjectManagementApp && npm run build && npm run test:run` - both must
   stay green (no `npm run lint` exists in this repo - don't invent a step for it).
