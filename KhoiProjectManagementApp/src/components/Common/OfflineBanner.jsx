import React from 'react';
import { WifiOff } from 'lucide-react';
import { useOnlineStatus } from '../../utils/useOnlineStatus';

// Persistent, dismissal-free banner shown whenever the browser reports no network interface at all -
// mounted once at the top of App (see App.jsx) so it's visible even on the login screen, not just
// once a user is authenticated. A slow-but-connected API is a different, per-request condition
// (surfaced via ApiService's NetworkError + each screen's existing ErrorMessage/retry pattern), not
// this banner's job.
const OfflineBanner = () => {
  const isOnline = useOnlineStatus();

  if (isOnline) return null;

  return (
    <div className="bg-yellow-50 border-b border-yellow-200 px-4 py-2 text-center text-sm text-yellow-800 flex items-center justify-center gap-2">
      <WifiOff className="h-4 w-4" />
      You're offline. Some actions won't work until your connection is back.
    </div>
  );
};

export default OfflineBanner;
