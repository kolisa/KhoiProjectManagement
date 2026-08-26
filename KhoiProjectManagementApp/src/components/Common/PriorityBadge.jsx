// src/components/Common/PriorityBadge.js
import React from 'react';

const priorityColors = {
  'low': 'bg-[#F2F2F4] text-[#62626A]',
  'medium': 'bg-[#FFEED6] text-[#874400]',
  'high': 'bg-[#FFEBE8] text-[#B71824]'
};

const PriorityBadge = ({ priority }) => {
  return (
    <span className={`inline-flex items-center px-[9px] py-[3px] rounded-[7px] text-[11.5px] font-semibold whitespace-nowrap ${priorityColors[priority]}`}>
      {priority}
    </span>
  );
};

export default PriorityBadge;
