// playwright.config.js
// A small, high-value slice per the test brief (login + one critical CRUD journey), not broad UI
// coverage - see e2e/*.spec.js.
//
// PREREQUISITES (not automated by this config - see the README this was built from):
//   1. `docker compose up -d` from the repo root (Postgres on localhost:5433)
//   2. `dotnet run --project KhoiProjectManagementApi` from the repo root (API on http://localhost:5278)
// This config only boots the Vite dev server (port 3000, matching Cors:AllowedOrigins in
// appsettings.json - `vite preview`'s default port 4173 is NOT in that allowlist, so `npm run dev` is
// used deliberately here instead of a production build+preview).
import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './e2e',
  // fullyParallel only serializes tests *within* one file - different spec files still land on
  // different workers by default and race each other against the one real, non-isolated dev backend
  // (unlike the Testcontainers-backed .NET tests, which get a fresh container but still serialize
  // within it for the same reason). Capping workers to 1 was the actual fix for flakiness observed
  // running login.spec.js and project-crud.spec.js concurrently against the shared dev API/DB.
  fullyParallel: false,
  workers: 1,
  retries: 0,
  // 'list' for readable console output; 'html' so CI's "Upload Playwright report" step has an
  // actual playwright-report/ directory to upload (it was previously always empty - see ci.yml) -
  // open: 'never' since a headless CI runner has no browser to auto-open a report in.
  reporter: [['list'], ['html', { open: 'never', outputFolder: 'playwright-report' }]],
  // Vite's dev server transforms modules on demand - the very first real navigation of a freshly
  // booted server (not yet warmed by an earlier run) can take longer than Playwright's 5s default
  // assertion timeout to finish transforming ~1900 modules, independent of anything the test is
  // asserting. 10s absorbs that cold-start cost without masking a genuine app-level regression.
  expect: { timeout: 10_000 },
  use: {
    baseURL: 'http://localhost:3000',
    trace: 'retain-on-failure',
  },
  webServer: {
    command: 'npm run dev',
    url: 'http://localhost:3000',
    reuseExistingServer: true,
    timeout: 60_000,
  },
  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } },
  ],
});
