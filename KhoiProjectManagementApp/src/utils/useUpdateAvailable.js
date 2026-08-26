// src/utils/useUpdateAvailable.js
import { useEffect, useState } from 'react';

// How often an already-open tab re-checks for a newer deploy, plus a check whenever the tab regains
// focus (the common case - most updates get noticed when someone switches back to a tab left open
// overnight, not while they're actively watching it).
const POLL_INTERVAL_MS = 5 * 60 * 1000;

// __APP_BUILD_ID__ is a per-build timestamp baked in by vite.config.js's define - a fresh `vite build`
// writes both a new value into the bundle and a matching build/version.json. This tab's bundle is
// frozen the moment it loaded; polling version.json is the only way it can learn a newer one now
// exists on the server. `cache: 'no-store'` matters here - a normal cached fetch would just keep
// returning this same tab's own (now stale) version.json forever.
export const useUpdateAvailable = () => {
  const [updateAvailable, setUpdateAvailable] = useState(false);

  useEffect(() => {
    let cancelled = false;

    const check = async () => {
      try {
        const response = await fetch('/version.json', { cache: 'no-store' });
        if (!response.ok) return;
        const data = await response.json();
        if (!cancelled && data?.buildId && data.buildId !== __APP_BUILD_ID__) {
          setUpdateAvailable(true);
        }
      } catch {
        // No version.json (dev server, or a flaky request) - nothing to report, try again next tick.
      }
    };

    check();
    const intervalId = setInterval(check, POLL_INTERVAL_MS);

    const onVisible = () => {
      if (document.visibilityState === 'visible') check();
    };
    document.addEventListener('visibilitychange', onVisible);

    return () => {
      cancelled = true;
      clearInterval(intervalId);
      document.removeEventListener('visibilitychange', onVisible);
    };
  }, []);

  return updateAvailable;
};
