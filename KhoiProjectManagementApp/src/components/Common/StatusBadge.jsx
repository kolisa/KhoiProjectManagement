// src/components/Common/StatusBadge.js
// Generic status pill - unlike PriorityBadge (a fixed low/medium/high palette), status vocabularies
// differ per feature (reminders: Pending/Completed/Snoozed), so the caller supplies its own colorMap.
import React from 'react';

const DEFAULT_COLORS = 'bg-gray-50 text-gray-700';

const StatusBadge = ({ status, colorMap = {} }) => (
  <span className={`inline-flex items-center px-2.5 py-1 rounded-full text-xs font-semibold ${colorMap[status] || DEFAULT_COLORS}`}>
    {status}
  </span>
);

export default StatusBadge;
