// src/components/Settings/NotificationPreferences.js
import React, { useState, useEffect } from 'react';
import { Bell, Mail } from 'lucide-react';

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
        <h2 className="text-3xl font-bold text-gray-900 flex items-center">
          <Bell className="h-7 w-7 mr-2 text-gray-700" />
          Notification Settings
        </h2>
        <p className="text-gray-600">Choose which events email you. In-app notifications always happen either way.</p>
      </div>

      <div className="bg-white rounded-lg shadow divide-y max-w-2xl">
        {preferences.map((pref) => (
          <div key={pref.notificationType} className="p-4 flex items-center justify-between">
            <div className="flex items-start">
              <Mail className="h-5 w-5 mr-3 mt-0.5 text-gray-400 flex-shrink-0" />
              <div>
                <div className="font-medium text-gray-900">{pref.displayName}</div>
                <div className="text-sm text-gray-500">{pref.description}</div>
              </div>
            </div>
            <label className="relative inline-flex items-center cursor-pointer flex-shrink-0 ml-4">
              <input
                type="checkbox"
                className="sr-only peer"
                checked={pref.emailEnabled}
                disabled={saving}
                onChange={(e) => handleToggle(pref.notificationType, e.target.checked)}
              />
              <div className="w-11 h-6 bg-gray-200 peer-focus:outline-none rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:border-gray-300 after:border after:rounded-full after:h-5 after:w-5 after:transition-all peer-checked:bg-blue-600"></div>
            </label>
          </div>
        ))}
      </div>
    </div>
  );
};

export default NotificationPreferences;
