// src/components/Calendar/CalendarPage.jsx
import React, { useState, useEffect, useCallback } from 'react';
import { Calendar as CalendarIcon, Plus, ChevronLeft, ChevronRight, Cake, Award, Megaphone, Zap, Pencil, Trash2, Rss } from 'lucide-react';
import { hasPermission } from '../../utils/permissions';
import { useToast } from '../../contexts/ToastContext';
import { reportApiError } from '../../utils/apiError';
import StatusBadge from '../Common/StatusBadge';
import EventForm from './EventForm';
import CalendarSubscribeModal from './CalendarSubscribeModal';

// One source of truth per type: badge color (StatusBadge's colorMap), the left-border accent each
// row gets so a scan down the list reads by type at a glance, and the stat-card icon/color above.
const TYPE_META = {
  Event: { badge: 'bg-[#EEEEFF] text-[#4131B0]', border: 'border-l-[#4131B0]', icon: CalendarIcon, iconColor: 'text-[#4131B0]', iconBg: 'bg-[#EEEEFF]' },
  Promotion: { badge: 'bg-[#E3F8E9] text-[#005F2E]', border: 'border-l-[#005F2E]', icon: Award, iconColor: 'text-[#005F2E]', iconBg: 'bg-[#E3F8E9]' },
  Marketing: { badge: 'bg-[#FFF1E3] text-[#B75E00]', border: 'border-l-[#B75E00]', icon: Megaphone, iconColor: 'text-[#B75E00]', iconBg: 'bg-[#FFF1E3]' },
  Activation: { badge: 'bg-[#FFE8F3] text-[#A3115C]', border: 'border-l-[#A3115C]', icon: Zap, iconColor: 'text-[#A3115C]', iconBg: 'bg-[#FFE8F3]' },
  Birthday: { badge: 'bg-[#FFEBE8] text-[#B71824]', border: 'border-l-[#B71824]', icon: Cake, iconColor: 'text-[#B71824]', iconBg: 'bg-[#FFEBE8]' },
};
const TYPE_ORDER = ['Event', 'Promotion', 'Marketing', 'Activation', 'Birthday'];

const monthLabel = (date) => date.toLocaleDateString(undefined, { month: 'long', year: 'numeric' });
const startOfMonth = (date) => new Date(date.getFullYear(), date.getMonth(), 1);
const endOfMonth = (date) => new Date(date.getFullYear(), date.getMonth() + 1, 0, 23, 59, 59);

const CalendarPage = ({ apiService, user }) => {
  const toast = useToast();
  const [monthAnchor, setMonthAnchor] = useState(() => new Date());
  const [feed, setFeed] = useState(null);
  const [error, setError] = useState(null);
  const [activeFilter, setActiveFilter] = useState(null);
  const [showForm, setShowForm] = useState(false);
  const [editingEvent, setEditingEvent] = useState(null);
  const [showSubscribe, setShowSubscribe] = useState(false);
  const [users, setUsers] = useState([]);

  const canManage = hasPermission(user?.permissions, 'calendar.manage');

  const loadFeed = useCallback(async () => {
    try {
      const result = await apiService.getCalendarFeed(startOfMonth(monthAnchor), endOfMonth(monthAnchor));
      setFeed(result);
      setError(null);
    } catch (err) {
      setError(err.message);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [monthAnchor.getFullYear(), monthAnchor.getMonth()]);

  useEffect(() => {
    loadFeed();
  }, [loadFeed]);

  useEffect(() => {
    if (canManage) {
      apiService.getUsers().then((list) => setUsers(list || [])).catch(() => {});
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [canManage]);

  // Merge events + computed birthdays into one date-sorted list of entries, each carrying a `type`
  // for filtering/badging - birthdays have no backend EventType (they're computed, never stored).
  const allEntries = (() => {
    if (!feed) return null;
    const eventEntries = feed.events.map((e) => ({
      kind: 'event',
      date: new Date(e.eventDate),
      type: e.eventType,
      title: e.title,
      description: e.description,
      subjectName: e.subjectName,
      creatorName: e.creatorName,
      raw: e,
    }));
    const birthdayEntries = feed.birthdays.map((b) => ({
      kind: 'birthday',
      date: new Date(monthAnchor.getFullYear(), b.month - 1, b.day),
      type: 'Birthday',
      title: `${b.name}'s birthday`,
      raw: b,
    }));
    return [...eventEntries, ...birthdayEntries].sort((a, b) => a.date - b.date);
  })();

  const entries = allEntries === null ? null : (activeFilter ? allEntries.filter((e) => e.type === activeFilter) : allEntries);
  const countsByType = Object.fromEntries(TYPE_ORDER.map((t) => [t, allEntries?.filter((e) => e.type === t).length ?? 0]));

  const handleCreate = async (dto) => {
    await apiService.createCalendarEvent(dto);
    setShowForm(false);
    await loadFeed();
    toast.success('Event created.');
  };

  const handleUpdate = async (dto) => {
    await apiService.updateCalendarEvent(editingEvent.id, dto);
    setEditingEvent(null);
    await loadFeed();
    toast.success('Event updated.');
  };

  const handleDelete = async (event) => {
    try {
      await apiService.deleteCalendarEvent(event.id);
      await loadFeed();
      toast.success('Event deleted.');
    } catch (err) {
      reportApiError(toast, err, 'Could not delete this event.');
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex justify-between items-center">
        <div>
          <h2 className="text-[27px] font-bold text-gray-900 flex items-center">
            <CalendarIcon className="h-7 w-7 mr-2 text-gray-700" />
            Calendar
          </h2>
          <p className="text-gray-600">Company events, promotions, birthdays, and Marketing/Activations campaigns</p>
        </div>
        <div className="flex items-center gap-3">
          <button
            onClick={() => setShowSubscribe(true)}
            className="inline-flex items-center gap-2 bg-white text-gray-700 border border-gray-300 px-4 py-2.5 rounded-[10px] text-sm font-semibold hover:bg-gray-50 shadow-sm transition-colors"
          >
            <Rss className="h-4 w-4" />
            Subscribe
          </button>
          {canManage && (
            <button
              onClick={() => setShowForm(true)}
              className="inline-flex items-center gap-2 bg-blue-600 text-white px-4 py-2.5 rounded-[10px] text-sm font-semibold hover:bg-blue-700 shadow-sm transition-colors"
            >
              <Plus className="h-4 w-4" />
              New Event
            </button>
          )}
        </div>
      </div>

      <div className="flex items-center justify-between bg-white rounded-2xl border border-gray-100 shadow-sm p-3">
        <button
          onClick={() => setMonthAnchor((d) => new Date(d.getFullYear(), d.getMonth() - 1, 1))}
          className="p-2 rounded-lg hover:bg-gray-100 text-gray-600 transition-colors"
          aria-label="Previous month"
        >
          <ChevronLeft className="h-5 w-5" />
        </button>
        <span className="font-semibold text-gray-900">{monthLabel(monthAnchor)}</span>
        <button
          onClick={() => setMonthAnchor((d) => new Date(d.getFullYear(), d.getMonth() + 1, 1))}
          className="p-2 rounded-lg hover:bg-gray-100 text-gray-600 transition-colors"
          aria-label="Next month"
        >
          <ChevronRight className="h-5 w-5" />
        </button>
      </div>

      <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-5 gap-3">
        {TYPE_ORDER.map((type) => {
          const meta = TYPE_META[type];
          const Icon = meta.icon;
          const isActive = activeFilter === type;
          return (
            <button
              key={type}
              onClick={() => setActiveFilter(isActive ? null : type)}
              className={`bg-white rounded-2xl border shadow-sm p-4 text-left hover:shadow-md transition-shadow ${
                isActive ? 'border-blue-500 ring-1 ring-blue-100' : 'border-gray-100'
              }`}
            >
              <div className={`inline-flex ${meta.iconBg} rounded-lg p-2 mb-2`}>
                <Icon className={`h-5 w-5 ${meta.iconColor}`} />
              </div>
              <div className="text-2xl font-bold text-gray-900">{countsByType[type]}</div>
              <div className="text-xs text-gray-500">{type === 'Event' ? 'Events' : type === 'Promotion' ? 'Promotions' : type === 'Activation' ? 'Activations' : type}</div>
            </button>
          );
        })}
      </div>

      {error && <div className="text-red-600 text-sm bg-red-50 border border-red-200 rounded-lg p-3">Error: {error}</div>}

      <div className="bg-white rounded-2xl border border-gray-100 shadow-sm divide-y divide-gray-100 overflow-hidden">
        {entries === null && (
          <div className="p-6 space-y-3" aria-busy="true" aria-label="Loading calendar">
            {[...Array(4)].map((_, i) => (
              <div key={i} className="h-12 bg-gray-100 rounded-lg animate-pulse" />
            ))}
          </div>
        )}

        {entries?.length === 0 && (
          <div className="p-10 text-center text-gray-400">
            <CalendarIcon className="h-10 w-10 mx-auto mb-3 text-gray-300" />
            <p>No events this month{activeFilter ? ' matching your filter' : ''}.</p>
            {activeFilter && (
              <button onClick={() => setActiveFilter(null)} className="text-blue-600 hover:text-blue-800 text-sm mt-2">
                Clear filter
              </button>
            )}
          </div>
        )}

        {entries?.map((entry, i) => {
          const meta = TYPE_META[entry.type] || TYPE_META.Event;
          return (
            <div
              key={`${entry.kind}-${entry.raw.id ?? entry.raw.userId}-${i}`}
              className={`p-4 flex items-start justify-between gap-4 border-l-4 ${meta.border} hover:bg-gray-50/60 transition-colors`}
            >
              <div className="flex items-start gap-3">
                <div className={`text-center w-12 flex-shrink-0 rounded-xl py-1.5 ${meta.iconBg}`}>
                  <div className={`text-[11px] uppercase font-semibold ${meta.iconColor}`}>{entry.date.toLocaleDateString(undefined, { month: 'short' })}</div>
                  <div className="text-lg font-bold text-gray-900">{entry.date.getDate()}</div>
                </div>
                <div>
                  <div className="flex items-center gap-2 flex-wrap">
                    <span className="font-medium text-gray-900">{entry.title}</span>
                    <StatusBadge status={entry.type} colorMap={{ [entry.type]: meta.badge }} />
                  </div>
                  {entry.description && <p className="text-sm text-gray-500 mt-1">{entry.description}</p>}
                  {entry.subjectName && <p className="text-sm text-gray-500 mt-1">Congratulations, {entry.subjectName}!</p>}
                </div>
              </div>
              {canManage && entry.kind === 'event' && (
                <div className="flex items-center gap-1 flex-shrink-0">
                  <button
                    onClick={() => setEditingEvent(entry.raw)}
                    className="p-1.5 rounded-md text-gray-400 hover:text-gray-700 hover:bg-gray-100 transition-colors"
                    aria-label={`Edit ${entry.title}`}
                  >
                    <Pencil className="h-4 w-4" />
                  </button>
                  <button
                    onClick={() => handleDelete(entry.raw)}
                    className="p-1.5 rounded-md text-gray-400 hover:text-red-600 hover:bg-red-50 transition-colors"
                    aria-label={`Delete ${entry.title}`}
                  >
                    <Trash2 className="h-4 w-4" />
                  </button>
                </div>
              )}
            </div>
          );
        })}
      </div>

      {showForm && (
        <EventForm users={users} onSave={handleCreate} onClose={() => setShowForm(false)} />
      )}
      {editingEvent && (
        <EventForm event={editingEvent} users={users} onSave={handleUpdate} onClose={() => setEditingEvent(null)} />
      )}
      {showSubscribe && (
        <CalendarSubscribeModal apiService={apiService} onClose={() => setShowSubscribe(false)} />
      )}
    </div>
  );
};

export default CalendarPage;
