// src/components/Common/StatusBadge.js
// Generic status pill - unlike PriorityBadge (a fixed low/medium/high palette), status vocabularies
// differ per feature (reminders: Pending/Completed/Snoozed), so the caller supplies its own colorMap.
import React from 'react';

const DEFAULT_COLORS = 'bg-gray-100 text-gray-800';

const StatusBadge = ({ status, colorMap = {} }) => (
  <span className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ${colorMap[status] || DEFAULT_COLORS}`}>
    {status}
  </span>
);

export default StatusBadge;
