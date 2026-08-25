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
      include: [
        'src/utils/**',
        'src/services/wikiHub.js',
        'src/services/ApiService.js',
        'src/App.jsx',
        'src/components/Auth/ForgotPasswordForm.jsx',
        'src/components/Auth/ResetPasswordForm.jsx',
        'src/components/Common/OfflineBanner.jsx',
      ],
      // A regression floor, not a target - set a few points below the actual measured baseline
      // (56.0% lines / 75.8% branches / 31.5% functions / 56.0% statements as of this pass, after
      // adding ApiService.js/OfflineBanner.jsx) so `npm run test:coverage` fails the build if a future
      // change drops coverage meaningfully, without being so tight it flakes on minor fluctuation.
      // Function coverage is the lowest of the four because ApiService.js has ~100 one-line endpoint
      // wrapper methods (createTask/getProjects/etc.) that all funnel through the well-tested
      // request()/fetchWithTimeout core - not independently worth a test each. Re-measure and adjust
      // whenever the include set changes materially.
      thresholds: { lines: 50, branches: 70, functions: 28, statements: 50 },
    },
  },
});
