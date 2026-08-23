import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
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
