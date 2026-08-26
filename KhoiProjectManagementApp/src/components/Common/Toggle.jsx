// src/components/Common/Toggle.js
// Shared toggle switch - extracted from the peer/after: markup that used to be duplicated (at two
// slightly different sizes) across NotificationPreferences.js and DashboardWidgetSettings.js.
import React from 'react';

const Toggle = ({ checked, onChange, disabled = false }) => (
  <label className="relative inline-flex items-center cursor-pointer flex-shrink-0">
    <input
      type="checkbox"
      className="sr-only peer"
      checked={checked}
      disabled={disabled}
      onChange={(e) => onChange(e.target.checked)}
    />
    <div className="w-10 h-[22px] bg-gray-200 peer-focus:ring-2 peer-focus:ring-blue-500 peer-focus:ring-offset-1 rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:border-gray-300 after:border after:rounded-full after:h-[18px] after:w-[18px] after:transition-all after:shadow-sm peer-checked:bg-blue-600 transition-colors"></div>
  </label>
);

export default Toggle;
