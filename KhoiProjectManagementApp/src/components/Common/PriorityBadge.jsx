// src/components/Common/PriorityBadge.js
import React from 'react';

const PriorityBadge = ({ priority }) => {
  const priorityColors = {
    'low': 'bg-gray-100 text-gray-800',
    'medium': 'bg-yellow-100 text-yellow-800',
    'high': 'bg-red-100 text-red-800'
  };
  
  return (
    <span className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ${priorityColors[priority]}`}>
      {priority}
    </span>
  );
};

export default PriorityBadge;
