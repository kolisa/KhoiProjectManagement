// src/components/Reminders/ReminderFilters.js
import React, { useState, useEffect, useRef } from 'react';
import { Search, X, SlidersHorizontal } from 'lucide-react';

const SEARCH_DEBOUNCE_MS = 350;

// assignedToId is only shown when the caller can see everyone's reminders (reminders.view_all) - for
// everyone else the list is already implicitly scoped to their own reminders server-side, so a filter
// that could only ever match themselves would be pointless UI.
const ReminderFilters = ({ filters, onChange, onReset, users, canViewAll }) => {
  const [searchInput, setSearchInput] = useState(filters.search || '');
  const [expanded, setExpanded] = useState(false);
  const debounceRef = useRef(null);

  useEffect(() => {
    setSearchInput(filters.search || '');
  }, [filters.search]);

  useEffect(() => {
    if (debounceRef.current) clearTimeout(debounceRef.current);
    debounceRef.current = setTimeout(() => {
      if (searchInput !== (filters.search || '')) onChange({ search: searchInput || undefined });
    }, SEARCH_DEBOUNCE_MS);
    return () => clearTimeout(debounceRef.current);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [searchInput]);

  const activeCount = Object.values(filters).filter((v) => v !== undefined && v !== null && v !== '').length;

  return (
    <div className="bg-white rounded-xl border border-gray-100 shadow-sm p-4 space-y-3">
      <div className="flex items-center space-x-3">
        <div className="relative flex-1">
          <Search className="h-4 w-4 absolute left-3 top-1/2 -translate-y-1/2 text-gray-400" />
          <input
            type="text"
            value={searchInput}
            onChange={(e) => setSearchInput(e.target.value)}
            placeholder="Search reminders by title or description..."
            aria-label="Search reminders"
            className="w-full border border-gray-300 rounded-lg pl-9 pr-9 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow"
          />
          {searchInput && (
            <button
              onClick={() => setSearchInput('')}
              className="absolute right-3 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600"
              aria-label="Clear search"
            >
              <X className="h-4 w-4" />
            </button>
          )}
        </div>
        <button
          onClick={() => setExpanded(!expanded)}
          className="flex items-center text-sm text-gray-600 hover:text-gray-900 border border-gray-300 rounded-lg px-3 py-2 hover:bg-gray-50 transition-colors"
          aria-expanded={expanded}
        >
          <SlidersHorizontal className="h-4 w-4 mr-1.5" />
          Filters{activeCount > 0 && ` (${activeCount})`}
        </button>
        {activeCount > 0 && (
          <button onClick={onReset} className="text-sm text-blue-600 hover:text-blue-800">
            Reset filters
          </button>
        )}
      </div>

      {expanded && (
        <div className="grid grid-cols-2 md:grid-cols-4 gap-3 pt-2 border-t">
          <div>
            <label className="block text-xs text-gray-500 mb-1" htmlFor="reminder-filter-status">Status</label>
            <select
              id="reminder-filter-status"
              value={filters.status || ''}
              onChange={(e) => onChange({ status: e.target.value || undefined })}
              className="w-full border border-gray-300 rounded-lg px-2 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow"
            >
              <option value="">All</option>
              <option value="Pending">Pending</option>
              <option value="Snoozed">Snoozed</option>
              <option value="Completed">Completed</option>
            </select>
          </div>
          <div>
            <label className="block text-xs text-gray-500 mb-1" htmlFor="reminder-filter-priority">Priority</label>
            <select
              id="reminder-filter-priority"
              value={filters.priority || ''}
              onChange={(e) => onChange({ priority: e.target.value || undefined })}
              className="w-full border border-gray-300 rounded-lg px-2 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow"
            >
              <option value="">All</option>
              <option value="low">Low</option>
              <option value="medium">Medium</option>
              <option value="high">High</option>
            </select>
          </div>
          <div>
            <label className="block text-xs text-gray-500 mb-1" htmlFor="reminder-filter-category">Category</label>
            <input
              id="reminder-filter-category"
              type="text"
              value={filters.category || ''}
              onChange={(e) => onChange({ category: e.target.value || undefined })}
              placeholder="Any"
              className="w-full border border-gray-300 rounded-lg px-2 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow"
            />
          </div>
          {canViewAll && (
            <div>
              <label className="block text-xs text-gray-500 mb-1" htmlFor="reminder-filter-assigned">Assigned to</label>
              <select
                id="reminder-filter-assigned"
                value={filters.assignedToId || ''}
                onChange={(e) => onChange({ assignedToId: e.target.value || undefined })}
                className="w-full border border-gray-300 rounded-lg px-2 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow"
              >
                <option value="">Anyone</option>
                {users?.map((u) => (
                  <option key={u.id} value={u.id}>{u.name}</option>
                ))}
              </select>
            </div>
          )}
          <div>
            <label className="block text-xs text-gray-500 mb-1" htmlFor="reminder-filter-due-from">Due from</label>
            <input
              id="reminder-filter-due-from"
              type="date"
              value={filters.dueFrom ? filters.dueFrom.slice(0, 10) : ''}
              onChange={(e) => onChange({ dueFrom: e.target.value ? new Date(e.target.value).toISOString() : undefined })}
              className="w-full border border-gray-300 rounded-lg px-2 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow"
            />
          </div>
          <div>
            <label className="block text-xs text-gray-500 mb-1" htmlFor="reminder-filter-due-to">Due to</label>
            <input
              id="reminder-filter-due-to"
              type="date"
              value={filters.dueTo ? filters.dueTo.slice(0, 10) : ''}
              onChange={(e) => onChange({ dueTo: e.target.value ? new Date(e.target.value).toISOString() : undefined })}
              className="w-full border border-gray-300 rounded-lg px-2 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow"
            />
          </div>
        </div>
      )}
    </div>
  );
};

export default ReminderFilters;
