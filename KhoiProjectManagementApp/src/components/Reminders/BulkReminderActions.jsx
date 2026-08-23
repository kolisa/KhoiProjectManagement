// src/components/Reminders/BulkReminderActions.js
import React, { useState } from 'react';
import { CheckCircle, Trash2, Clock, Flag, X } from 'lucide-react';

const BulkReminderActions = ({ count, onComplete, onDelete, onReschedule, onPriority, onClear }) => {
  const [confirmingDelete, setConfirmingDelete] = useState(false);
  const [showReschedule, setShowReschedule] = useState(false);
  const [rescheduleValue, setRescheduleValue] = useState('');

  if (count === 0) return null;

  return (
    <div className="bg-blue-50 border border-blue-200 rounded-lg p-3 flex flex-wrap items-center gap-2">
      <span className="text-sm font-medium text-blue-900 mr-2">{count} selected</span>

      <button onClick={onComplete} className="flex items-center text-sm bg-white border rounded-lg px-3 py-1.5 hover:bg-gray-50">
        <CheckCircle className="h-4 w-4 mr-1.5 text-green-600" />
        Complete
      </button>

      <div className="relative">
        <button onClick={() => setShowReschedule(!showReschedule)} className="flex items-center text-sm bg-white border rounded-lg px-3 py-1.5 hover:bg-gray-50">
          <Clock className="h-4 w-4 mr-1.5 text-gray-600" />
          Reschedule
        </button>
        {showReschedule && (
          <div className="absolute mt-1 bg-white border rounded-lg shadow-lg p-2 z-10 flex space-x-1">
            <input
              type="datetime-local"
              value={rescheduleValue}
              onChange={(e) => setRescheduleValue(e.target.value)}
              className="border rounded px-1 py-0.5 text-xs"
            />
            <button
              onClick={() => { if (rescheduleValue) { onReschedule(new Date(rescheduleValue).toISOString()); setShowReschedule(false); } }}
              className="text-xs bg-blue-600 text-white px-2 rounded"
            >
              Set
            </button>
          </div>
        )}
      </div>

      <select
        onChange={(e) => { if (e.target.value) { onPriority(e.target.value); e.target.value = ''; } }}
        defaultValue=""
        className="text-sm bg-white border rounded-lg px-3 py-1.5"
        aria-label="Change priority for selected reminders"
      >
        <option value="" disabled>
          <Flag className="h-4 w-4" /> Change priority...
        </option>
        <option value="low">Set Low</option>
        <option value="medium">Set Medium</option>
        <option value="high">Set High</option>
      </select>

      {confirmingDelete ? (
        <div className="flex items-center space-x-2 bg-red-50 border border-red-200 rounded-lg px-2 py-1">
          <span className="text-sm text-red-800">Delete {count} reminders?</span>
          <button onClick={() => { onDelete(); setConfirmingDelete(false); }} className="text-sm font-medium text-red-700 hover:text-red-900">
            Confirm
          </button>
          <button onClick={() => setConfirmingDelete(false)} className="text-sm text-gray-500 hover:text-gray-700">
            Cancel
          </button>
        </div>
      ) : (
        <button onClick={() => setConfirmingDelete(true)} className="flex items-center text-sm bg-white border rounded-lg px-3 py-1.5 hover:bg-red-50 text-red-600">
          <Trash2 className="h-4 w-4 mr-1.5" />
          Delete
        </button>
      )}

      <button onClick={onClear} className="ml-auto text-gray-400 hover:text-gray-600" aria-label="Clear selection">
        <X className="h-4 w-4" />
      </button>
    </div>
  );
};

export default BulkReminderActions;
