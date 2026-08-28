---
name: frontend-quality-gate
description: Run the KhoiProjectManagementApp frontend's quality checks (build, tests, coverage) and report actual results. Use before considering frontend work done, or when asked to verify the frontend is in a good state.
---

# Frontend quality gate (Khoi Pro)

Run from `KhoiProjectManagementApp/`. There is **no `npm run lint`** in this repo (CRA's
`eslintConfig` was removed along with `react-scripts` during the Vite migration) - don't invent or
run one.

```bash
cd KhoiProjectManagementApp
npm install          # only if dependencies changed
npm run test:run     # Vitest, all *.test.js/*.test.jsx
npm run build        # vite build -> build/ (not dist/ - vite.config.js pins outDir)
```

Optional, for coverage numbers specifically (this is what CI's `frontend` job also runs):
```bash
npm run test:coverage
```

E2E (Playwright) is a separate, heavier step - only run it if specifically asked, since it needs a
live backend + Postgres running (`playwright.config.js`'s `webServer` block starts the Vite dev
server itself, but not the API):
```bash
npx playwright test
```

## What "green" looks like right now (baseline as of 2026-08-28)

- `npm run build`: succeeds, ~1900 modules, one expected warning about a 628KB chunk exceeding the
  raised 600KB limit (`vite.config.js` comment explains this is accepted for now, not a regression).
- `npm run test:run`: 14 test files, 91 tests, all passing. Some `act(...)` warnings print from
  `App.test.jsx` (React state updates outside `act()`) - these are warnings, not failures; don't
  treat stderr noise as a failing run, but also don't add new ones carelessly.

## Report format

State the actual command run and actual result - pass/fail counts, warning text if any. **Never
report a check as passed without having run it.** If `npm install` wasn't needed, say so rather
than running it anyway. If Playwright wasn't run because no live backend was available, say that
explicitly rather than omitting it silently.

## Stop and report failures

If `npm run build` or `npm run test:run` fails, stop and report the actual error - don't attempt to
silently work around it (e.g. by skipping a failing test file) unless asked to fix it.
