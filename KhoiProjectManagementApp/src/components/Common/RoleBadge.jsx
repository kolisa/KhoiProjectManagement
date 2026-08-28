// src/components/Common/RoleBadge.js
import React from 'react';
import { Shield, UserCheck, User } from 'lucide-react';

const RoleBadge = ({ role }) => {
  const roleColors = {
    'admin': 'bg-purple-100 text-purple-800',
    'manager': 'bg-blue-100 text-blue-800',
    'member': 'bg-green-100 text-green-800'
  };
  
  const roleIcons = {
    'admin': Shield,
    'manager': UserCheck,
    'member': User
  };
  
  // An unrecognized role has no icon/color entry - fall back rather than rendering <undefined />,
  // which React throws on.
  const Icon = roleIcons[role] || User;
  const colorClass = roleColors[role] || 'bg-gray-100 text-gray-800';

  return (
    <span className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ${colorClass}`}>
      <Icon className="w-3 h-3 mr-1" />
      {role}
    </span>
  );
};

export default RoleBadge;