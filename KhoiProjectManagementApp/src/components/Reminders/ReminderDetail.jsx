// src/components/Reminders/ReminderDetail.js
import React, { useState } from 'react';
import { X, Edit3, Trash2, CheckCircle, RotateCcw, Copy, RefreshCw, Clock } from 'lucide-react';
import PriorityBadge from '../Common/PriorityBadge';
import StatusBadge from '../Common/StatusBadge';

const STATUS_COLORS = {
  Pending: 'bg-blue-100 text-blue-800',
  Snoozed: 'bg-purple-100 text-purple-800',
  Completed: 'bg-green-100 text-green-800',
};

const formatDateTime = (iso) => (iso ? new Date(iso).toLocaleString() : '-');

const ReminderDetail = ({ reminder, onClose, onEdit, onComplete, onReopen, onDelete, onDuplicate }) => {
  const [confirmingDelete, setConfirmingDelete] = useState(false);

  return (
    <div className="bg-white rounded-lg shadow p-6">
      <div className="flex justify-between items-start mb-4">
        <div>
          <h3 className="text-xl font-semibold text-gray-900">{reminder.title}</h3>
          <div className="flex items-center space-x-2 mt-2">
            <PriorityBadge priority={reminder.priority} />
            <StatusBadge status={reminder.status} colorMap={STATUS_COLORS} />
            {reminder.recurrenceType && (
              <span className="inline-flex items-center text-xs bg-indigo-50 text-indigo-700 px-2 py-0.5 rounded-full">
                <RefreshCw className="h-3 w-3 mr-1" />
                {reminder.recurrenceType}
              </span>
            )}
            {reminder.isOverdue && (
              <span className="text-xs bg-red-100 text-red-800 px-2 py-0.5 rounded-full">Overdue</span>
            )}
          </div>
        </div>
        <button onClick={onClose} className="text-gray-400 hover:text-gray-600" aria-label="Close">
          <X className="h-4 w-4" />
        </button>
      </div>

      {reminder.description && <p className="text-gray-700 text-sm mb-4 whitespace-pre-wrap">{reminder.description}</p>}

      <dl className="grid grid-cols-2 gap-3 text-sm mb-5">
        <div>
          <dt className="text-gray-400">Due</dt>
          <dd className="text-gray-900 flex items-center"><Clock className="h-3.5 w-3.5 mr-1" />{formatDateTime(reminder.dueAt)}</dd>
        </div>
        {reminder.snoozedUntil && (
          <div>
            <dt className="text-gray-400">Snoozed until</dt>
            <dd className="text-gray-900">{formatDateTime(reminder.snoozedUntil)}</dd>
          </div>
        )}
        <div>
          <dt className="text-gray-400">Assigned to</dt>
          <dd className="text-gray-900">{reminder.assignedToName}</dd>
        </div>
        <div>
          <dt className="text-gray-400">Created by</dt>
          <dd className="text-gray-900">{reminder.createdByName}</dd>
        </div>
        {reminder.category && (
          <div>
            <dt className="text-gray-400">Category</dt>
            <dd className="text-gray-900">{reminder.category}</dd>
          </div>
        )}
        {reminder.relatedProjectName && (
          <div>
            <dt className="text-gray-400">Related project</dt>
            <dd className="text-gray-900">{reminder.relatedProjectName}</dd>
          </div>
        )}
        <div>
          <dt className="text-gray-400">Created</dt>
          <dd className="text-gray-900">{formatDateTime(reminder.createdAt)}</dd>
        </div>
        {reminder.updatedAt && (
          <div>
            <dt className="text-gray-400">Last updated</dt>
            <dd className="text-gray-900">{formatDateTime(reminder.updatedAt)}</dd>
          </div>
        )}
        {reminder.completedAt && (
          <div>
            <dt className="text-gray-400">Completed</dt>
            <dd className="text-gray-900">{formatDateTime(reminder.completedAt)}</dd>
          </div>
        )}
      </dl>

      <div className="flex flex-wrap gap-2 border-t pt-4">
        {reminder.status !== 'Completed' ? (
          <button onClick={onComplete} className="flex items-center text-sm bg-green-600 text-white px-3 py-1.5 rounded-lg hover:bg-green-700">
            <CheckCircle className="h-4 w-4 mr-1.5" />
            Complete
          </button>
        ) : (
          <button onClick={onReopen} className="flex items-center text-sm bg-gray-100 text-gray-700 px-3 py-1.5 rounded-lg hover:bg-gray-200">
            <RotateCcw className="h-4 w-4 mr-1.5" />
            Reopen
          </button>
        )}
        <button onClick={onEdit} className="flex items-center text-sm bg-white border px-3 py-1.5 rounded-lg hover:bg-gray-50">
          <Edit3 className="h-4 w-4 mr-1.5" />
          Edit
        </button>
        <button onClick={onDuplicate} className="flex items-center text-sm bg-white border px-3 py-1.5 rounded-lg hover:bg-gray-50">
          <Copy className="h-4 w-4 mr-1.5" />
          Duplicate
        </button>
        {confirmingDelete ? (
          <div className="flex items-center space-x-2 bg-red-50 border border-red-200 rounded-lg px-2 py-1">
            <span className="text-sm text-red-800">Delete this reminder?</span>
            <button onClick={onDelete} className="text-sm font-medium text-red-700 hover:text-red-900">Confirm</button>
            <button onClick={() => setConfirmingDelete(false)} className="text-sm text-gray-500 hover:text-gray-700">Cancel</button>
          </div>
        ) : (
          <button onClick={() => setConfirmingDelete(true)} className="flex items-center text-sm text-red-600 border border-red-200 px-3 py-1.5 rounded-lg hover:bg-red-50">
            <Trash2 className="h-4 w-4 mr-1.5" />
            Delete
          </button>
        )}
      </div>
    </div>
  );
};

export default ReminderDetail;
