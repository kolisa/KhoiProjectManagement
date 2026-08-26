// src/components/Reminders/ReminderList.js
import React, { useState } from 'react';
import { Clock, CheckCircle, RotateCcw, MoreVertical } from 'lucide-react';
import PriorityBadge from '../Common/PriorityBadge';
import StatusBadge from '../Common/StatusBadge';

const STATUS_COLORS = {
  Pending: 'bg-blue-50 text-blue-700',
  Snoozed: 'bg-purple-50 text-purple-700',
  Completed: 'bg-green-50 text-green-700',
};

const formatDue = (iso) => {
  const d = new Date(iso);
  return d.toLocaleString(undefined, { month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit' });
};

const SnoozeMenu = ({ onSnooze, onClose }) => {
  const options = [
    { label: 'Later today', hours: 3 },
    { label: 'Tomorrow', hours: 24 },
    { label: 'Next week', hours: 24 * 7 },
  ];
  const [customValue, setCustomValue] = useState('');

  return (
    <div className="absolute right-0 mt-1 bg-white border border-gray-100 rounded-xl shadow-lg z-10 w-48 py-1" role="menu">
      {options.map((o) => (
        <button
          key={o.label}
          role="menuitem"
          onClick={() => { onSnooze(new Date(Date.now() + o.hours * 3600 * 1000).toISOString()); onClose(); }}
          className="w-full text-left px-3 py-2 text-sm hover:bg-gray-50"
        >
          {o.label}
        </button>
      ))}
      <div className="px-3 py-2 border-t">
        <label className="block text-xs text-gray-500 mb-1" htmlFor="snooze-custom">Custom</label>
        <div className="flex space-x-1">
          <input
            id="snooze-custom"
            type="datetime-local"
            value={customValue}
            onChange={(e) => setCustomValue(e.target.value)}
            className="flex-1 border rounded px-1 py-0.5 text-xs"
          />
          <button
            onClick={() => { if (customValue) { onSnooze(new Date(customValue).toISOString()); onClose(); } }}
            className="text-xs bg-blue-600 text-white px-2 rounded"
          >
            Set
          </button>
        </div>
      </div>
    </div>
  );
};

const ReminderList = ({ reminders, selectedId, selectedIds, onSelect, onToggleSelect, onToggleSelectAll, onComplete, onReopen, onSnooze }) => {
  const [snoozeMenuId, setSnoozeMenuId] = useState(null);
  const allSelected = reminders.length > 0 && reminders.every((r) => selectedIds.includes(r.id));

  return (
    <div className="bg-white rounded-xl border border-gray-100 shadow-sm overflow-hidden">
      {/* Desktop table */}
      <table className="w-full text-sm hidden md:table">
        <thead className="bg-gray-50 border-b text-left text-xs text-gray-500 uppercase">
          <tr>
            <th className="px-3 py-2 w-8">
              <input type="checkbox" checked={allSelected} onChange={onToggleSelectAll} aria-label="Select all reminders" />
            </th>
            <th className="px-3 py-2">Title</th>
            <th className="px-3 py-2">Due</th>
            <th className="px-3 py-2">Priority</th>
            <th className="px-3 py-2">Status</th>
            <th className="px-3 py-2">Assigned to</th>
            <th className="px-3 py-2 text-right">Actions</th>
          </tr>
        </thead>
        <tbody className="divide-y">
          {reminders.map((r) => (
            <tr
              key={r.id}
              onClick={() => onSelect(r.id)}
              className={`cursor-pointer hover:bg-gray-50/60 transition-colors ${selectedId === r.id ? 'bg-blue-50' : ''} ${r.isOverdue ? 'bg-red-50/40' : ''}`}
            >
              <td className="px-3 py-2" onClick={(e) => e.stopPropagation()}>
                <input
                  type="checkbox"
                  checked={selectedIds.includes(r.id)}
                  onChange={() => onToggleSelect(r.id)}
                  aria-label={`Select ${r.title}`}
                />
              </td>
              <td className="px-3 py-2">
                <div className="font-medium text-gray-900">{r.title}</div>
                {r.category && <div className="text-xs text-gray-400">{r.category}</div>}
              </td>
              <td className={`px-3 py-2 whitespace-nowrap ${r.isOverdue ? 'text-red-600 font-medium' : 'text-gray-600'}`}>
                <Clock className="h-3.5 w-3.5 inline mr-1 -mt-0.5" />
                {formatDue(r.dueAt)}
              </td>
              <td className="px-3 py-2"><PriorityBadge priority={r.priority} /></td>
              <td className="px-3 py-2"><StatusBadge status={r.status} colorMap={STATUS_COLORS} /></td>
              <td className="px-3 py-2 text-gray-600">{r.assignedToName}</td>
              <td className="px-3 py-2" onClick={(e) => e.stopPropagation()}>
                <div className="flex items-center justify-end space-x-1 relative">
                  {r.status !== 'Completed' ? (
                    <button
                      onClick={() => onComplete(r.id)}
                      className="text-green-600 hover:text-green-800 hover:bg-gray-100 p-1 rounded-md transition-colors"
                      aria-label={`Mark ${r.title} complete`}
                      title="Complete"
                    >
                      <CheckCircle className="h-4 w-4" />
                    </button>
                  ) : (
                    <button
                      onClick={() => onReopen(r.id)}
                      className="text-gray-500 hover:text-gray-700 hover:bg-gray-100 p-1 rounded-md transition-colors"
                      aria-label={`Reopen ${r.title}`}
                      title="Reopen"
                    >
                      <RotateCcw className="h-4 w-4" />
                    </button>
                  )}
                  {r.status !== 'Completed' && (
                    <button
                      onClick={() => setSnoozeMenuId(snoozeMenuId === r.id ? null : r.id)}
                      className="text-gray-400 hover:text-gray-600 hover:bg-gray-100 p-1 rounded-md transition-colors"
                      aria-label={`More actions for ${r.title}`}
                      aria-haspopup="menu"
                    >
                      <MoreVertical className="h-4 w-4" />
                    </button>
                  )}
                  {snoozeMenuId === r.id && (
                    <SnoozeMenu onSnooze={(until) => onSnooze(r.id, until)} onClose={() => setSnoozeMenuId(null)} />
                  )}
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </table>

      {/* Mobile cards */}
      <div className="md:hidden divide-y divide-gray-100">
        {reminders.map((r) => (
          <div
            key={r.id}
            onClick={() => onSelect(r.id)}
            className={`p-4 cursor-pointer hover:bg-gray-50/60 transition-colors ${selectedId === r.id ? 'bg-blue-50' : ''} ${r.isOverdue ? 'bg-red-50/40' : ''}`}
          >
            <div className="flex items-start justify-between">
              <div className="flex items-start space-x-2 min-w-0">
                <input
                  type="checkbox"
                  checked={selectedIds.includes(r.id)}
                  onChange={(e) => { e.stopPropagation(); onToggleSelect(r.id); }}
                  onClick={(e) => e.stopPropagation()}
                  className="mt-1"
                  aria-label={`Select ${r.title}`}
                />
                <div className="min-w-0">
                  <div className="font-medium text-gray-900 truncate">{r.title}</div>
                  <div className={`text-xs mt-0.5 ${r.isOverdue ? 'text-red-600 font-medium' : 'text-gray-500'}`}>
                    {formatDue(r.dueAt)} &middot; {r.assignedToName}
                  </div>
                </div>
              </div>
              {r.status !== 'Completed' ? (
                <button onClick={(e) => { e.stopPropagation(); onComplete(r.id); }} className="text-green-600 hover:bg-gray-100 p-1 rounded-md transition-colors" aria-label={`Mark ${r.title} complete`}>
                  <CheckCircle className="h-5 w-5" />
                </button>
              ) : (
                <button onClick={(e) => { e.stopPropagation(); onReopen(r.id); }} className="text-gray-500 hover:bg-gray-100 p-1 rounded-md transition-colors" aria-label={`Reopen ${r.title}`}>
                  <RotateCcw className="h-5 w-5" />
                </button>
              )}
            </div>
            <div className="flex items-center space-x-2 mt-2">
              <PriorityBadge priority={r.priority} />
              <StatusBadge status={r.status} colorMap={STATUS_COLORS} />
            </div>
          </div>
        ))}
      </div>
    </div>
  );
};

export default ReminderList;
