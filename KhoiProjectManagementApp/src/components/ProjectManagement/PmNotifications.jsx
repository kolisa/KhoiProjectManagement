import React, { useState, useEffect, useCallback, useRef } from 'react';
import { Bell } from 'lucide-react';

const POLL_MS = 60000;

const when = (iso) => new Date(iso).toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' });

// A small notification bell for the module-only shell. Accounts confined to Project Management never see
// the main app's header, so without this the mentions and comments addressed to them would be invisible.
const PmNotifications = ({ apiService }) => {
  const [items, setItems] = useState([]);
  const [open, setOpen] = useState(false);
  const rootRef = useRef(null);

  const load = useCallback(async () => {
    try {
      const list = await apiService.getNotifications();
      setItems((list || []).slice().sort((a, b) => new Date(b.createdAt) - new Date(a.createdAt)).slice(0, 20));
    } catch {
      // A failed refresh just leaves the previous list in place; the next poll retries.
    }
  }, [apiService]);

  useEffect(() => {
    load();
    const timer = setInterval(load, POLL_MS);
    return () => clearInterval(timer);
  }, [load]);

  // Close on outside click / Escape.
  useEffect(() => {
    if (!open) return undefined;
    const onDown = (e) => { if (rootRef.current && !rootRef.current.contains(e.target)) setOpen(false); };
    const onKey = (e) => { if (e.key === 'Escape') setOpen(false); };
    document.addEventListener('mousedown', onDown);
    document.addEventListener('keydown', onKey);
    return () => {
      document.removeEventListener('mousedown', onDown);
      document.removeEventListener('keydown', onKey);
    };
  }, [open]);

  const markRead = async (n) => {
    if (n.isRead) return;
    setItems((prev) => prev.map((x) => (x.id === n.id ? { ...x, isRead: true } : x)));
    try {
      await apiService.markNotificationAsRead(n.id);
    } catch {
      setItems((prev) => prev.map((x) => (x.id === n.id ? { ...x, isRead: false } : x)));
    }
  };

  const unread = items.filter((n) => !n.isRead).length;

  return (
    <div className="relative" ref={rootRef}>
      <button onClick={() => setOpen((o) => !o)} aria-haspopup="true" aria-expanded={open}
        aria-label={unread ? `Notifications, ${unread} unread` : 'Notifications'}
        className="relative inline-flex items-center text-blue-100 hover:text-white">
        <Bell className="h-5 w-5" aria-hidden="true" />
        {unread > 0 && (
          <span className="absolute -top-1.5 -right-1.5 min-w-[1.1rem] h-[1.1rem] px-1 rounded-full bg-red-500 text-white text-[10px] font-bold flex items-center justify-center">{unread > 9 ? '9+' : unread}</span>
        )}
      </button>
      {open && (
        <div role="region" aria-label="Notifications" className="absolute right-0 mt-2 w-80 max-w-[90vw] bg-white text-gray-900 rounded-xl shadow-xl border border-gray-200 z-50">
          <div className="px-4 py-2.5 border-b border-gray-100 text-sm font-semibold">Notifications</div>
          {items.length === 0 ? (
            <p className="px-4 py-6 text-sm text-gray-500 text-center">You're all caught up.</p>
          ) : (
            <ul className="max-h-80 overflow-y-auto divide-y divide-gray-100">
              {items.map((n) => (
                <li key={n.id}>
                  <button onClick={() => markRead(n)} className={`w-full text-left px-4 py-2.5 hover:bg-gray-50 ${n.isRead ? '' : 'bg-blue-50/60'}`}>
                    <span className={`block text-sm ${n.isRead ? 'text-gray-600' : 'font-medium text-gray-900'}`}>{n.message}</span>
                    <span className="block text-xs text-gray-500 mt-0.5">{when(n.createdAt)}</span>
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>
      )}
    </div>
  );
};

export default PmNotifications;
