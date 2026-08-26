// src/components/Settings/NotificationPreferences.js
import React, { useState, useEffect } from 'react';
import { Bell, Mail } from 'lucide-react';
import Toggle from '../Common/Toggle';

const NotificationPreferences = ({ apiService }) => {
  const [preferences, setPreferences] = useState(null);
  const [error, setError] = useState(null);
  const [saving, setSaving] = useState(false);

  const load = async () => {
    try {
      const result = await apiService.getNotificationPreferences();
      setPreferences(result || []);
    } catch (err) {
      setError(err.message);
    }
  };

  useEffect(() => {
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const handleToggle = async (notificationType, emailEnabled) => {
    setPreferences((prev) =>
      prev.map((p) => (p.notificationType === notificationType ? { ...p, emailEnabled } : p))
    );
    setSaving(true);
    try {
      await apiService.setNotificationPreferences([{ notificationType, emailEnabled }]);
    } catch (err) {
      setError(err.message);
      // Revert on failure so the toggle doesn't lie about what's actually saved.
      setPreferences((prev) =>
        prev.map((p) => (p.notificationType === notificationType ? { ...p, emailEnabled: !emailEnabled } : p))
      );
    } finally {
      setSaving(false);
    }
  };

  if (error) return <div className="p-4 text-red-600">Error: {error}</div>;
  if (!preferences) return <div className="p-4 text-gray-400">Loading preferences...</div>;

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-[27px] font-bold text-gray-900 flex items-center">
          <Bell className="h-7 w-7 mr-2 text-gray-700" />
          Notification Settings
        </h2>
        <p className="text-gray-600">Choose which events email you. In-app notifications always happen either way.</p>
      </div>

      <div className="bg-white rounded-2xl border border-gray-100 shadow-sm divide-y divide-gray-100 max-w-2xl">
        {preferences.map((pref) => (
          <div key={pref.notificationType} className="p-4 flex items-center justify-between">
            <div className="flex items-start">
              <Mail className="h-5 w-5 mr-3 mt-0.5 text-gray-400 flex-shrink-0" />
              <div>
                <div className="font-medium text-gray-900">{pref.displayName}</div>
                <div className="text-sm text-gray-500">{pref.description}</div>
              </div>
            </div>
            <div className="ml-4">
              <Toggle
                checked={pref.emailEnabled}
                disabled={saving}
                onChange={(checked) => handleToggle(pref.notificationType, checked)}
              />
            </div>
          </div>
        ))}
      </div>
    </div>
  );
};

export default NotificationPreferences;
