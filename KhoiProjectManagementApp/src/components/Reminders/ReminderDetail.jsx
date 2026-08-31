// src/components/Reminders/ReminderDetail.js
import React, { useState } from 'react';
import { X, Edit3, Trash2, CheckCircle, RotateCcw, Copy, Clock } from 'lucide-react';
import PriorityBadge from '../Common/PriorityBadge';
import StatusBadge from '../Common/StatusBadge';

const STATUS_COLORS = {
  Pending: 'bg-[#EEEEFF] text-[#4131B0]',
  Snoozed: 'bg-[#FFEED6] text-[#874400]',
  Completed: 'bg-[#E3F8E9] text-[#005F2E]',
};
const RECURRENCE_COLOR = 'bg-[#EEEEFF] text-[#4131B0]';
const OVERDUE_COLOR = 'bg-[#FFEBE8] text-[#B71824]';

const formatDateTime = (iso) => (iso ? new Date(iso).toLocaleString() : '-');

const ReminderDetail = ({ reminder, onClose, onEdit, onComplete, onReopen, onDelete, onDuplicate }) => {
  const [confirmingDelete, setConfirmingDelete] = useState(false);

  return (
    <div className="bg-white rounded-2xl border border-gray-100 shadow-sm p-6">
      <div className="flex justify-between items-start mb-4">
        <div>
          <h3 className="text-xl font-semibold text-gray-900">{reminder.title}</h3>
          <div className="flex items-center space-x-2 mt-2">
            <PriorityBadge priority={reminder.priority} />
            <StatusBadge status={reminder.status} colorMap={STATUS_COLORS} />
            {reminder.recurrenceType && (
              <StatusBadge status={reminder.recurrenceType} colorMap={{ [reminder.recurrenceType]: RECURRENCE_COLOR }} />
            )}
            {reminder.isOverdue && (
              <StatusBadge status="Overdue" colorMap={{ Overdue: OVERDUE_COLOR }} />
            )}
          </div>
        </div>
        <button onClick={onClose} className="text-gray-400 hover:text-gray-600 hover:bg-gray-100 p-1 rounded-md transition-colors" aria-label="Close">
          <X className="h-4 w-4" />
        </button>
      </div>

      {reminder.description && <p className="text-gray-700 text-sm mb-4 whitespace-pre-wrap">{reminder.description}</p>}

      <dl className="grid grid-cols-2 gap-3 text-sm mb-5">
        <div>
          <dt className="text-gray-500">Due</dt>
          <dd className="text-gray-900 flex items-center"><Clock className="h-3.5 w-3.5 mr-1" />{formatDateTime(reminder.dueAt)}</dd>
        </div>
        {reminder.snoozedUntil && (
          <div>
            <dt className="text-gray-500">Snoozed until</dt>
            <dd className="text-gray-900">{formatDateTime(reminder.snoozedUntil)}</dd>
          </div>
        )}
        <div>
          <dt className="text-gray-500">Assigned to</dt>
          <dd className="text-gray-900">{reminder.assignedToName}</dd>
        </div>
        <div>
          <dt className="text-gray-500">Created by</dt>
          <dd className="text-gray-900">{reminder.createdByName}</dd>
        </div>
        {reminder.category && (
          <div>
            <dt className="text-gray-500">Category</dt>
            <dd className="text-gray-900">{reminder.category}</dd>
          </div>
        )}
        {reminder.relatedProjectName && (
          <div>
            <dt className="text-gray-500">Related project</dt>
            <dd className="text-gray-900">{reminder.relatedProjectName}</dd>
          </div>
        )}
        <div>
          <dt className="text-gray-500">Created</dt>
          <dd className="text-gray-900">{formatDateTime(reminder.createdAt)}</dd>
        </div>
        {reminder.updatedAt && (
          <div>
            <dt className="text-gray-500">Last updated</dt>
            <dd className="text-gray-900">{formatDateTime(reminder.updatedAt)}</dd>
          </div>
        )}
        {reminder.completedAt && (
          <div>
            <dt className="text-gray-500">Completed</dt>
            <dd className="text-gray-900">{formatDateTime(reminder.completedAt)}</dd>
          </div>
        )}
      </dl>

      <div className="flex flex-wrap gap-2 border-t pt-4">
        {reminder.status !== 'Completed' ? (
          <button onClick={onComplete} className="inline-flex items-center gap-2 bg-green-600 text-white px-4 py-2.5 rounded-[10px] text-sm font-semibold shadow-sm hover:bg-green-700 transition-colors">
            <CheckCircle className="h-4 w-4" />
            Complete
          </button>
        ) : (
          <button onClick={onReopen} className="inline-flex items-center gap-2 bg-gray-100 text-gray-700 px-4 py-2.5 rounded-[10px] text-sm font-semibold shadow-sm hover:bg-gray-200 transition-colors">
            <RotateCcw className="h-4 w-4" />
            Reopen
          </button>
        )}
        <button onClick={onEdit} className="inline-flex items-center gap-2 border border-gray-300 bg-white px-4 py-2.5 rounded-[10px] text-sm font-semibold hover:bg-gray-50 transition-colors">
          <Edit3 className="h-4 w-4" />
          Edit
        </button>
        <button onClick={onDuplicate} className="inline-flex items-center gap-2 border border-gray-300 bg-white px-4 py-2.5 rounded-[10px] text-sm font-semibold hover:bg-gray-50 transition-colors">
          <Copy className="h-4 w-4" />
          Duplicate
        </button>
        {confirmingDelete ? (
          <div className="flex items-center space-x-2 bg-red-50 border border-red-200 rounded-lg px-2 py-1">
            <span className="text-sm text-red-800">Delete this reminder?</span>
            <button onClick={onDelete} className="text-sm font-medium text-red-700 hover:text-red-900">Confirm</button>
            <button onClick={() => setConfirmingDelete(false)} className="text-sm text-gray-500 hover:text-gray-700">Cancel</button>
          </div>
        ) : (
          <button onClick={() => setConfirmingDelete(true)} className="inline-flex items-center gap-2 border border-red-200 text-red-600 px-4 py-2.5 rounded-[10px] text-sm font-semibold hover:bg-red-50 transition-colors">
            <Trash2 className="h-4 w-4" />
            Delete
          </button>
        )}
      </div>
    </div>
  );
};

export default ReminderDetail;
