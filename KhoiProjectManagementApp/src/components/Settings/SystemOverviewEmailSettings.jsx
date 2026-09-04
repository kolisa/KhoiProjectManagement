// src/components/Settings/SystemOverviewEmailSettings.jsx
// Admin-only: on/off switch + day/time for the weekly "about the system" email (see
// SystemOverviewEmailJob). Modeled as a day of week + a time of day, not raw cron syntax - that's
// genuinely all this schedule is. A change takes effect immediately (no restart/redeploy needed) -
// see JobRescheduler on the backend.
import React, { useState, useEffect } from 'react';
import { Clock } from 'lucide-react';
import Toggle from '../Common/Toggle';
import { useToast } from '../../contexts/ToastContext';
import { reportApiError } from '../../utils/apiError';

// Matches JS Date.getDay()'s own 0-6 numbering, so the API's int-serialized DayOfWeek needs no
// translation table.
const DAY_LABELS = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];

const toTimeInputValue = (hour, minute) =>
  `${String(hour).padStart(2, '0')}:${String(minute).padStart(2, '0')}`;

const SystemOverviewEmailSettings = ({ apiService }) => {
  const toast = useToast();
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [enabled, setEnabled] = useState(true);
  const [dayOfWeek, setDayOfWeek] = useState(5);
  const [time, setTime] = useState('10:00');
  const [lastChanged, setLastChanged] = useState(null);

  const applySettings = (settings) => {
    setEnabled(settings.enabled);
    setDayOfWeek(settings.dayOfWeek);
    setTime(toTimeInputValue(settings.hour, settings.minute));
    setLastChanged(
      settings.updatedByUserName
        ? { name: settings.updatedByUserName, at: settings.updatedAtUtc }
        : null
    );
  };

  useEffect(() => {
    apiService
      .getSystemOverviewEmailSettings()
      .then(applySettings)
      .catch((err) => reportApiError(toast, err, 'Could not load the system overview email settings.'))
      .finally(() => setLoading(false));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const handleSave = async () => {
    const [hourStr, minuteStr] = time.split(':');
    setSaving(true);
    try {
      const result = await apiService.updateSystemOverviewEmailSettings({
        enabled,
        dayOfWeek: Number(dayOfWeek),
        hour: Number(hourStr),
        minute: Number(minuteStr),
      });
      applySettings(result);
      toast.success('System overview email schedule saved.');
    } catch (err) {
      reportApiError(toast, err, 'Could not save the schedule.');
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return null;
  }

  return (
    <div className="space-y-4">
      <div>
        <h3 className="text-lg font-semibold text-gray-900 flex items-center">
          <Clock className="h-5 w-5 mr-2 text-gray-700" />
          System Overview Email
        </h3>
        <p className="text-sm text-gray-500">
          A weekly email explaining what KhoiHub is, its features, and how to get help.
        </p>
      </div>

      <div className="flex items-center gap-3">
        <Toggle checked={enabled} disabled={saving} onChange={setEnabled} />
        <span className="text-sm text-gray-700">{enabled ? 'Enabled' : 'Disabled'}</span>
      </div>

      <div className="flex flex-wrap items-end gap-4">
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1" htmlFor="overview-email-day">Day</label>
          <select
            id="overview-email-day"
            value={dayOfWeek}
            disabled={saving}
            onChange={(e) => setDayOfWeek(Number(e.target.value))}
            className="border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
          >
            {DAY_LABELS.map((label, index) => (
              <option key={label} value={index}>{label}</option>
            ))}
          </select>
        </div>

        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1" htmlFor="overview-email-time">Time</label>
          <input
            id="overview-email-time"
            type="time"
            value={time}
            disabled={saving}
            onChange={(e) => setTime(e.target.value)}
            className="border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
          />
        </div>

        <button
          type="button"
          onClick={handleSave}
          disabled={saving}
          className="inline-flex items-center gap-2 bg-blue-600 text-white px-4 py-2.5 rounded-[10px] text-sm font-semibold hover:bg-blue-700 shadow-sm transition-colors disabled:opacity-50"
        >
          {saving ? 'Saving...' : 'Save'}
        </button>
      </div>

      {lastChanged && (
        <p className="text-xs text-gray-500">
          Last changed by {lastChanged.name} on {new Date(lastChanged.at).toLocaleString()}.
        </p>
      )}
    </div>
  );
};

export default SystemOverviewEmailSettings;
