import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import { writeFileSync } from 'fs';
import { join } from 'path';

// One id per `vite build` run, embedded into the bundle (via define, below) and also written out as
// its own static build/version.json - see src/utils/useUpdateAvailable.js. The running app polls
// version.json and compares it to the id baked into its own bundle; a mismatch means a newer build has
// been deployed since this tab loaded, which is the one thing a bundled id can't tell you about itself.
const buildId = String(Date.now());

// Writes build/version.json alongside the rest of the build output - `apply: 'build'` so this never
// runs (and never needs a version.json fallback) under `vite dev`, where there is no build output.
const writeVersionFile = () => ({
  name: 'write-version-file',
  apply: 'build',
  writeBundle(options) {
    writeFileSync(join(options.dir, 'version.json'), JSON.stringify({ buildId }));
  },
});

export default defineConfig({
  plugins: [react(), writeVersionFile()],
  define: {
    __APP_BUILD_ID__: JSON.stringify(buildId),
  },
  server: {
    port: 3000,
  },
  build: {
    // Keep the CRA-era output folder name so nothing downstream (deploy scripts, .gitignore) needs to
    // change - Vite's own default is "dist".
    outDir: 'build',
    // The single-bundle build is ~509kB (App.jsx is one large file, not code-split) - fine for an
    // internal tool. Raised past the default 500kB rather than silenced outright, so the warning still
    // fires for real if the bundle grows meaningfully past this.
    chunkSizeWarningLimit: 600,
    rollupOptions: {
      onwarn(warning, warn) {
        // @microsoft/signalr ships a /*#__PURE__*/ comment Rollup can't attach to a valid statement
        // (upstream packaging issue, not our code - Rollup just strips the annotation and continues,
        // no effect on the build's correctness). Narrowly scoped to that exact case so a genuine
        // INVALID_ANNOTATION warning from our own code would still surface.
        if (warning.code === 'INVALID_ANNOTATION' && warning.id?.includes('@microsoft/signalr')) {
          return;
        }
        warn(warning);
      },
    },
  },
});
