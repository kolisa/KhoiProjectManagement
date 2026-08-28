// src/components/Common/StatusBadge.js
// Generic status pill - unlike PriorityBadge (a fixed low/medium/high palette), status vocabularies
// differ per feature (reminders: Pending/Completed/Snoozed), so the caller supplies its own colorMap.
import React from 'react';

const DEFAULT_COLORS = 'bg-[#F2F2F4] text-[#62626A]';

// `label` is an optional display override - colorMap is always keyed by the raw `status` value, but
// a caller may want different display text (e.g. "in progress" for a "in-progress" status) without
// losing the color lookup, which pass-through-only `status` text can't do on its own.
const StatusBadge = ({ status, label, colorMap = {} }) => (
  <span className={`inline-flex items-center px-[9px] py-[3px] rounded-[7px] text-[11.5px] font-semibold whitespace-nowrap ${colorMap[status] || DEFAULT_COLORS}`}>
    {label ?? status}
  </span>
);

export default StatusBadge;
