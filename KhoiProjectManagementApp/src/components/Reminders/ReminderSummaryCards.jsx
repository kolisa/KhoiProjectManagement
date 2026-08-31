// src/components/Reminders/ReminderSummaryCards.js
import React from 'react';
import { ListChecks, CalendarClock, CalendarDays, AlertTriangle, CheckCircle2, Flame } from 'lucide-react';

const CARDS = [
  { key: 'active', label: 'Active', icon: ListChecks, countKey: 'totalActive', color: 'text-blue-600', bg: 'bg-blue-50', view: null },
  { key: 'today', label: 'Due Today', icon: CalendarClock, countKey: 'dueToday', color: 'text-amber-600', bg: 'bg-amber-50', view: 'today' },
  { key: 'upcoming', label: 'Upcoming', icon: CalendarDays, countKey: 'upcoming', color: 'text-indigo-600', bg: 'bg-indigo-50', view: 'upcoming' },
  { key: 'overdue', label: 'Overdue', icon: AlertTriangle, countKey: 'overdue', color: 'text-red-600', bg: 'bg-red-50', view: 'overdue' },
  { key: 'completed', label: 'Completed', icon: CheckCircle2, countKey: 'completed', color: 'text-green-600', bg: 'bg-green-50', view: 'completed' },
  { key: 'high', label: 'High Priority', icon: Flame, countKey: 'highPriority', color: 'text-orange-600', bg: 'bg-orange-50', view: null, priority: 'high' },
];

const ReminderSummaryCards = ({ summary, activeView, activePriority, onSelect }) => {
  if (!summary) return null;

  return (
    <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-6 gap-3">
      {CARDS.map(({ key, label, icon: Icon, countKey, color, bg, view, priority }) => {
        const isActive = priority ? activePriority === priority : (view ?? null) === (activeView ?? null) && !activePriority;
        return (
          <button
            key={key}
            onClick={() => onSelect({ view: view ?? null, priority: priority ?? null })}
            className={`bg-white rounded-2xl border shadow-sm p-4 text-left hover:shadow-md transition-shadow ${
              isActive ? 'border-blue-500 ring-1 ring-blue-100' : 'border-gray-100'
            }`}
          >
            <div className={`inline-flex ${bg} rounded-lg p-2 mb-2`}>
              <Icon className={`h-5 w-5 ${color}`} />
            </div>
            <div className="text-2xl font-bold text-gray-900">{summary[countKey] ?? 0}</div>
            <div className="text-xs text-gray-500">{label}</div>
          </button>
        );
      })}
    </div>
  );
};

export default ReminderSummaryCards;
