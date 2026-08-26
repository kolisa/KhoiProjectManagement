// src/components/Common/PriorityBadge.js
import React from 'react';

const priorityColors = {
  'low': 'bg-gray-50 text-gray-700',
  'medium': 'bg-amber-50 text-amber-700',
  'high': 'bg-red-50 text-red-700'
};

const priorityDotColors = {
  'low': 'bg-gray-400',
  'medium': 'bg-amber-500',
  'high': 'bg-red-500'
};

const PriorityBadge = ({ priority }) => {
  return (
    <span className={`inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-semibold ${priorityColors[priority]}`}>
      <span className={`w-1.5 h-1.5 rounded-full ${priorityDotColors[priority]}`} />
      {priority}
    </span>
  );
};

export default PriorityBadge;
