// src/components/Wiki/WikiPresence.js
// Renders who's currently viewing a wiki page (avatar initials) and, if someone holds the edit lock,
// a banner naming them. Purely presentational - WikiPageDetail owns the SignalR connection and state.
import React from 'react';
import { Lock } from 'lucide-react';

const initials = (name) =>
  (name || '?')
    .split(' ')
    .map((p) => p[0])
    .filter(Boolean)
    .slice(0, 2)
    .join('')
    .toUpperCase();

const WikiPresence = ({ viewers, editLock, currentUserId }) => {
  const others = viewers.filter((v) => v.userId !== currentUserId);

  return (
    <div className="flex items-center flex-wrap gap-2 mb-2">
      {viewers.length > 0 && (
        <div className="flex items-center -space-x-1.5">
          {viewers.map((v) => (
            <div
              key={v.userId}
              title={v.userId === currentUserId ? `${v.userName} (you)` : v.userName}
              className="h-6 w-6 rounded-full bg-blue-50 text-blue-700 text-[10px] font-semibold flex items-center justify-center border-2 border-white shadow-sm"
            >
              {initials(v.userName)}
            </div>
          ))}
        </div>
      )}
      {others.length > 0 && (
        <span className="text-xs text-gray-400">
          {others.length === 1 ? `${others[0].userName} is also viewing` : `${others.length} others viewing`}
        </span>
      )}
      {editLock && editLock.userId !== currentUserId && (
        <span className="inline-flex items-center gap-1.5 text-xs font-semibold text-amber-700 bg-amber-50 border border-amber-200 rounded-full px-2.5 py-1">
          <Lock className="h-3 w-3" />
          {editLock.userName} is editing
        </span>
      )}
    </div>
  );
};

export default WikiPresence;
