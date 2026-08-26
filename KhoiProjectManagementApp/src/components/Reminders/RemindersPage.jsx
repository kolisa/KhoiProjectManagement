// src/components/Reminders/RemindersPage.js
import React, { useState, useEffect, useCallback } from 'react';
import { Bell, Plus } from 'lucide-react';
import { hasPermission } from '../../utils/permissions';
import ReminderSummaryCards from './ReminderSummaryCards';
import ReminderFilters from './ReminderFilters';
import ReminderList from './ReminderList';
import ReminderForm from './ReminderForm';
import ReminderDetail from './ReminderDetail';
import BulkReminderActions from './BulkReminderActions';

const VIEW_TABS = [
  { key: null, label: 'All' },
  { key: 'today', label: 'Today' },
  { key: 'upcoming', label: 'Upcoming' },
  { key: 'overdue', label: 'Overdue' },
  { key: 'completed', label: 'Completed' },
];

// Filters live in the URL (?view=&status=&priority=...) via plain URLSearchParams + replaceState -
// this app has no router library, so a full history-stack push isn't attempted, just a shareable/
// refresh-safe query string, matching the deep-link technique WikiPage.js already uses elsewhere.
const readFiltersFromUrl = () => {
  const params = new URLSearchParams(window.location.search);
  if (params.get('tab') !== 'reminders') return {};
  const filters = {};
  ['view', 'status', 'priority', 'category', 'dueFrom', 'dueTo', 'assignedToId', 'search'].forEach((key) => {
    const value = params.get(key);
    if (value) filters[key] = value;
  });
  return filters;
};

const writeFiltersToUrl = (filters) => {
  const params = new URLSearchParams(window.location.search);
  params.set('tab', 'reminders');
  ['view', 'status', 'priority', 'category', 'dueFrom', 'dueTo', 'assignedToId', 'search'].forEach((key) => {
    if (filters[key]) params.set(key, filters[key]);
    else params.delete(key);
  });
  window.history.replaceState(null, '', `${window.location.pathname}?${params.toString()}`);
};

const RemindersPage = ({ apiService, user }) => {
  const [filters, setFilters] = useState(readFiltersFromUrl);
  const [reminders, setReminders] = useState(null);
  const [summary, setSummary] = useState(null);
  const [users, setUsers] = useState([]);
  const [projects, setProjects] = useState([]);
  const [error, setError] = useState(null);
  const [selectedId, setSelectedId] = useState(null);
  const [selectedIds, setSelectedIds] = useState([]);
  const [showForm, setShowForm] = useState(false);
  const [editingReminder, setEditingReminder] = useState(null);
  const [refreshing, setRefreshing] = useState(false);

  const canViewAll = hasPermission(user?.permissions, 'reminders.view_all');
  const canManage = hasPermission(user?.permissions, 'reminders.manage');

  const loadReminders = useCallback(async (background = false) => {
    if (background) setRefreshing(true);
    try {
      const result = await apiService.getReminders(filters);
      setReminders(result || []);
      setError(null);
    } catch (err) {
      setError(err.message);
    } finally {
      setRefreshing(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [JSON.stringify(filters)]);

  const loadSummary = useCallback(async () => {
    try {
      setSummary(await apiService.getReminderSummary());
    } catch {
      // Summary cards are a nice-to-have overview - a failure here shouldn't block the list itself.
    }
  }, [apiService]);

  useEffect(() => {
    loadReminders();
    loadSummary();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [JSON.stringify(filters)]);

  useEffect(() => {
    writeFiltersToUrl(filters);
  }, [filters]);

  useEffect(() => {
    Promise.all([apiService.getUsers(), apiService.getProjects()])
      .then(([userList, projectList]) => {
        setUsers(userList || []);
        setProjects(projectList || []);
      })
      .catch(() => {}); // used only to populate optional dropdowns - not worth surfacing as a page error
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const handleFilterChange = (patch) => setFilters((prev) => ({ ...prev, ...patch }));
  const handleReset = () => setFilters({});
  const handleCardSelect = ({ view, priority }) => setFilters((prev) => ({
    ...(view || priority ? {} : prev), // clicking "Active" clears other filters back to the base view
    view: view || undefined,
    priority: priority || undefined,
  }));

  const selectedReminder = reminders?.find((r) => r.id === selectedId) || null;

  const refreshAll = async () => {
    await Promise.all([loadReminders(true), loadSummary()]);
  };

  const handleCreate = async (dto) => {
    const created = await apiService.createReminder(dto);
    setShowForm(false);
    await refreshAll();
    setSelectedId(created.id);
  };

  const handleUpdate = async (dto) => {
    await apiService.updateReminder(editingReminder.id, dto);
    setEditingReminder(null);
    await refreshAll();
  };

  const handleComplete = async (id) => { await apiService.completeReminder(id); await refreshAll(); };
  const handleReopen = async (id) => { await apiService.reopenReminder(id); await refreshAll(); };
  const handleSnooze = async (id, until) => { await apiService.snoozeReminder(id, until); await refreshAll(); };
  const handleDuplicate = async () => {
    const copy = await apiService.duplicateReminder(selectedReminder.id);
    await refreshAll();
    setSelectedId(copy.id);
  };
  const handleDelete = async () => {
    await apiService.deleteReminder(selectedReminder.id);
    setSelectedId(null);
    await refreshAll();
  };

  const toggleSelect = (id) => setSelectedIds((prev) => (prev.includes(id) ? prev.filter((x) => x !== id) : [...prev, id]));
  const toggleSelectAll = () => {
    const ids = reminders.map((r) => r.id);
    setSelectedIds((prev) => (ids.every((id) => prev.includes(id)) ? [] : ids));
  };
  const clearSelection = () => setSelectedIds([]);

  const handleBulkComplete = async () => { await apiService.bulkCompleteReminders(selectedIds); clearSelection(); await refreshAll(); };
  const handleBulkDelete = async () => { await apiService.bulkDeleteReminders(selectedIds); clearSelection(); await refreshAll(); };
  const handleBulkReschedule = async (dueAt) => { await apiService.bulkRescheduleReminders(selectedIds, dueAt); clearSelection(); await refreshAll(); };
  const handleBulkPriority = async (priority) => { await apiService.bulkPriorityReminders(selectedIds, priority); clearSelection(); await refreshAll(); };

  return (
    <div className="space-y-6">
      <div className="flex justify-between items-center">
        <div>
          <h2 className="text-[27px] font-bold text-gray-900 flex items-center">
            <Bell className="h-7 w-7 mr-2 text-gray-700" />
            My Reminders
          </h2>
          <p className="text-gray-600">Stay on top of what's due, snooze what can wait, never lose track</p>
        </div>
        <button
          onClick={() => setShowForm(true)}
          className="bg-blue-600 text-white px-4 py-2 rounded-lg hover:bg-blue-700 flex items-center"
        >
          <Plus className="h-5 w-5 mr-2" />
          New Reminder
        </button>
      </div>

      <ReminderSummaryCards
        summary={summary}
        activeView={filters.view}
        activePriority={filters.priority}
        onSelect={handleCardSelect}
      />

      <div className="flex space-x-1 border-b">
        {VIEW_TABS.map((tab) => (
          <button
            key={tab.label}
            onClick={() => handleFilterChange({ view: tab.key || undefined, priority: undefined })}
            className={`px-4 py-2 text-sm font-medium border-b-2 -mb-px ${
              (filters.view || null) === tab.key && !filters.priority
                ? 'border-blue-600 text-blue-600 font-semibold'
                : 'border-transparent text-gray-500 hover:text-gray-700 transition-colors'
            }`}
          >
            {tab.label}
          </button>
        ))}
      </div>

      <ReminderFilters filters={filters} onChange={handleFilterChange} onReset={handleReset} users={users} canViewAll={canViewAll} />

      {selectedIds.length > 0 && (
        <BulkReminderActions
          count={selectedIds.length}
          onComplete={handleBulkComplete}
          onDelete={handleBulkDelete}
          onReschedule={handleBulkReschedule}
          onPriority={handleBulkPriority}
          onClear={clearSelection}
        />
      )}

      {error && <div className="text-red-600 text-sm bg-red-50 border border-red-200 rounded-lg p-3">Error: {error}</div>}

      <div className={`grid grid-cols-1 ${selectedReminder ? 'lg:grid-cols-3' : ''} gap-6`}>
        <div className={selectedReminder ? 'lg:col-span-2' : ''}>
          {reminders === null && (
            <div className="bg-white rounded-2xl border border-gray-100 shadow-sm p-6 space-y-3" aria-busy="true" aria-label="Loading reminders">
              {[...Array(4)].map((_, i) => (
                <div key={i} className="h-12 bg-gray-100 rounded-lg animate-pulse" />
              ))}
            </div>
          )}

          {reminders?.length === 0 && (
            <div className="bg-white rounded-2xl border border-gray-100 shadow-sm p-10 text-center text-gray-400">
              <Bell className="h-10 w-10 mx-auto mb-3 text-gray-300" />
              {Object.keys(filters).length > 0 ? (
                <>
                  <p>No reminders match your filters.</p>
                  <button onClick={handleReset} className="text-blue-600 hover:text-blue-800 text-sm mt-2">Reset filters</button>
                </>
              ) : (
                <>
                  <p>No reminders yet.</p>
                  <button onClick={() => setShowForm(true)} className="text-blue-600 hover:text-blue-800 text-sm mt-2">Create your first reminder</button>
                </>
              )}
            </div>
          )}

          {reminders?.length > 0 && (
            <div className={refreshing ? 'opacity-60 transition-opacity' : ''}>
              <ReminderList
                reminders={reminders}
                selectedId={selectedId}
                selectedIds={selectedIds}
                onSelect={setSelectedId}
                onToggleSelect={toggleSelect}
                onToggleSelectAll={toggleSelectAll}
                onComplete={handleComplete}
                onReopen={handleReopen}
                onSnooze={handleSnooze}
              />
            </div>
          )}
        </div>

        {selectedReminder && (
          <div className="lg:col-span-1">
            <ReminderDetail
              reminder={selectedReminder}
              onClose={() => setSelectedId(null)}
              onEdit={() => setEditingReminder(selectedReminder)}
              onComplete={() => handleComplete(selectedReminder.id)}
              onReopen={() => handleReopen(selectedReminder.id)}
              onDelete={handleDelete}
              onDuplicate={handleDuplicate}
            />
          </div>
        )}
      </div>

      {showForm && (
        <ReminderForm
          users={users}
          projects={projects}
          canAssignOthers={canManage}
          onSave={handleCreate}
          onClose={() => setShowForm(false)}
        />
      )}

      {editingReminder && (
        <ReminderForm
          reminder={editingReminder}
          users={users}
          projects={projects}
          canAssignOthers={canManage}
          onSave={handleUpdate}
          onClose={() => setEditingReminder(null)}
        />
      )}
    </div>
  );
};

export default RemindersPage;
