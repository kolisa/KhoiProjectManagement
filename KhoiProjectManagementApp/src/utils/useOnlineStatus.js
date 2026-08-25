// src/utils/useOnlineStatus.js
import { useEffect, useState } from 'react';

// navigator.onLine/the online/offline events only reflect whether the network *interface* is up
// (e.g. Wi-Fi connected), not whether the API is actually reachable - that's still surfaced per
// request via ApiService's NetworkError. This is deliberately just the coarse "you have no network
// interface at all" signal for the persistent banner (see components/Common/OfflineBanner.jsx).
export const useOnlineStatus = () => {
  const [isOnline, setIsOnline] = useState(() =>
    typeof navigator === 'undefined' ? true : navigator.onLine
  );

  useEffect(() => {
    const goOnline = () => setIsOnline(true);
    const goOffline = () => setIsOnline(false);

    window.addEventListener('online', goOnline);
    window.addEventListener('offline', goOffline);

    return () => {
      window.removeEventListener('online', goOnline);
      window.removeEventListener('offline', goOffline);
    };
  }, []);

  return isOnline;
};
