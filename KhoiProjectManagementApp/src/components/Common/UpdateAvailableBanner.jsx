import React from 'react';
import { RefreshCw } from 'lucide-react';
import { useUpdateAvailable } from '../../utils/useUpdateAvailable';

// Mounted once at the top of App (see App.jsx), same as OfflineBanner right next to it - visible
// regardless of auth state, so a tab left open on the login screen overnight still notices a new
// deploy. Unlike OfflineBanner this never clears itself once true: the new build is on the server for
// good until this tab reloads, so there's nothing to "go back to".
const UpdateAvailableBanner = () => {
  const updateAvailable = useUpdateAvailable();

  if (!updateAvailable) return null;

  return (
    <div className="bg-blue-50 border-b border-blue-200 px-4 py-2 text-center text-sm text-blue-800 flex items-center justify-center gap-3">
      <span className="flex items-center gap-2">
        <RefreshCw className="h-4 w-4" />
        A new version of KhoiHub is available.
      </span>
      <button
        onClick={() => window.location.reload()}
        className="font-semibold underline hover:text-blue-900 transition-colors"
      >
        Refresh to update
      </button>
    </div>
  );
};

export default UpdateAvailableBanner;
