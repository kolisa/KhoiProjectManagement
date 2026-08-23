// src/components/Settings/DashboardWidgetSettings.js
import React, { useState, useEffect } from 'react';
import { LayoutGrid, ArrowUp, ArrowDown } from 'lucide-react';
import { hasPermission } from '../../utils/permissions';

const DashboardWidgetSettings = ({ apiService, user }) => {
  const [prefs, setPrefs] = useState(null);
  const [catalog, setCatalog] = useState(null);
  const [error, setError] = useState(null);
  const [saving, setSaving] = useState(false);

  const canManageAllowlist = hasPermission(user?.permissions, 'dashboard.manage');

  const load = async () => {
    try {
      const [p, c] = await Promise.all([
        apiService.getMyDashboardWidgetPreferences(),
        canManageAllowlist ? apiService.getDashboardWidgetCatalog() : Promise.resolve(null),
      ]);
      setPrefs(p || []);
      setCatalog(c);
    } catch (err) {
      setError(err.message);
    }
  };

  useEffect(() => {
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const persist = async (nextPrefs) => {
    setSaving(true);
    try {
      await apiService.setMyDashboardWidgetPreferences(
        nextPrefs.map((p, i) => ({ widgetKey: p.widgetKey, isVisible: p.isVisible, sortOrder: i }))
      );
      setPrefs(nextPrefs.map((p, i) => ({ ...p, sortOrder: i })));
    } catch (err) {
      setError(err.message);
    } finally {
      setSaving(false);
    }
  };

  const toggleVisible = (widgetKey) => {
    const next = prefs.map((p) => (p.widgetKey === widgetKey ? { ...p, isVisible: !p.isVisible } : p));
    persist(next);
  };

  const move = (index, direction) => {
    const target = index + direction;
    if (target < 0 || target >= prefs.length) return;
    const next = [...prefs];
    [next[index], next[target]] = [next[target], next[index]];
    persist(next);
  };

  const toggleAllowlistEntry = async (widgetKey, isEnabled) => {
    setSaving(true);
    try {
      await apiService.setDashboardWidgetAllowlist([{ widgetKey, isEnabled }]);
      await load();
    } catch (err) {
      setError(err.message);
    } finally {
      setSaving(false);
    }
  };

  if (error) return <div className="text-red-600 text-sm">{error}</div>;
  if (!prefs) return <div className="text-gray-400 text-sm">Loading dashboard settings...</div>;

  return (
    <div className="space-y-6">
      <div>
        <h3 className="text-lg font-semibold text-gray-900 flex items-center">
          <LayoutGrid className="h-5 w-5 mr-2 text-gray-700" />
          Dashboard Widgets
        </h3>
        <p className="text-sm text-gray-500">Choose which widgets appear on your dashboard, and in what order.</p>
      </div>

      <div className="bg-white rounded-lg shadow divide-y max-w-2xl">
        {prefs.map((p, i) => (
          <div key={p.widgetKey} className="p-3 flex items-center justify-between">
            <div className="flex items-center">
              <div className="flex flex-col mr-3">
                <button
                  onClick={() => move(i, -1)}
                  disabled={i === 0 || saving}
                  className="text-gray-400 hover:text-gray-700 disabled:opacity-30"
                  aria-label="Move up"
                >
                  <ArrowUp className="h-3.5 w-3.5" />
                </button>
                <button
                  onClick={() => move(i, 1)}
                  disabled={i === prefs.length - 1 || saving}
                  className="text-gray-400 hover:text-gray-700 disabled:opacity-30"
                  aria-label="Move down"
                >
                  <ArrowDown className="h-3.5 w-3.5" />
                </button>
              </div>
              <span className="text-sm text-gray-900">{p.displayName}</span>
            </div>
            <label className="relative inline-flex items-center cursor-pointer">
              <input
                type="checkbox"
                className="sr-only peer"
                checked={p.isVisible}
                disabled={saving}
                onChange={() => toggleVisible(p.widgetKey)}
              />
              <div className="w-9 h-5 bg-gray-200 rounded-full peer peer-checked:after:translate-x-full after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:border-gray-300 after:border after:rounded-full after:h-4 after:w-4 after:transition-all peer-checked:bg-blue-600"></div>
            </label>
          </div>
        ))}
      </div>

      {canManageAllowlist && catalog && (
        <div>
          <h3 className="text-lg font-semibold text-gray-900 mt-6">Widget Availability (Admin)</h3>
          <p className="text-sm text-gray-500 mb-2">
            Turning a widget off here removes it for everyone, regardless of their personal preference.
          </p>
          <div className="bg-white rounded-lg shadow divide-y max-w-2xl">
            {catalog.map((c) => (
              <div key={c.widgetKey} className="p-3 flex items-center justify-between">
                <div>
                  <div className="text-sm text-gray-900">{c.displayName}</div>
                  <div className="text-xs text-gray-500">{c.description}</div>
                </div>
                <label className="relative inline-flex items-center cursor-pointer">
                  <input
                    type="checkbox"
                    className="sr-only peer"
                    checked={c.isEnabled}
                    disabled={saving}
                    onChange={() => toggleAllowlistEntry(c.widgetKey, !c.isEnabled)}
                  />
                  <div className="w-9 h-5 bg-gray-200 rounded-full peer peer-checked:after:translate-x-full after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:border-gray-300 after:border after:rounded-full after:h-4 after:w-4 after:transition-all peer-checked:bg-blue-600"></div>
                </label>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
};

export default DashboardWidgetSettings;
