import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// Separate from vite.config.js (rather than merging a `test` block into it) so `vite build`/`vite dev`
// never pick up Vitest-only config - kept minimal and mirrors vite.config.js's own plugin setup.
export default defineConfig({
  plugins: [react()],
  test: {
    // e2e/*.spec.js use @playwright/test, not Vitest - Vitest's default include glob would otherwise
    // also pick them up and fail to collect them (Playwright's test.describe() errors outside its own runner).
    exclude: ['**/node_modules/**', '**/e2e/**'],
    environment: 'jsdom',
    // The installed @testing-library/jest-dom is v5 (predates its dedicated Vitest entry point) - its
    // auto-extend script reaches for a global `expect`, which only exists with globals enabled.
    globals: true,
    setupFiles: ['src/test/setup.js'],
    css: true,
    coverage: {
      provider: 'v8',
      reporter: ['text', 'html'],
      // Business logic and the genuinely-live components/files - excludes AuthGuard.jsx/LoginForm.jsx,
      // which CLAUDE.md documents as dead code never imported anywhere (the real LoginForm/AuthGuard
      // live inline in App.jsx).
      include: ['src/utils/**', 'src/services/wikiHub.js', 'src/App.jsx', 'src/components/Auth/ForgotPasswordForm.jsx', 'src/components/Auth/ResetPasswordForm.jsx'],
      // A regression floor, not a target - set a few points below the actual measured baseline
      // (58.4% lines / 72.9% branches / 49.5% functions across this include set as of this pass) so
      // `npm run test:coverage` fails the build if a future change drops coverage meaningfully,
      // without being so tight it flakes on minor fluctuation.
      thresholds: { lines: 50, branches: 65, functions: 40, statements: 50 },
    },
  },
});
