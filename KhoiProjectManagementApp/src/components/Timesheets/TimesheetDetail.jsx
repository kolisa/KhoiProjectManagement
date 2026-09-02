// src/components/Timesheets/TimesheetDetail.jsx
import React, { useState } from 'react';
import { ArrowLeft, Plus, Trash2, Send, Check, Ban, Download } from 'lucide-react';
import { hasPermission } from '../../utils/permissions';
import { validateTimesheet, hasErrors } from '../../utils/validation';
import { useToast } from '../../contexts/ToastContext';
import { reportApiError } from '../../utils/apiError';
import StatusBadge from '../Common/StatusBadge';
import { downloadTimesheetCsv } from './timesheetExport';
import { splitHours, combineHours, formatDuration } from './duration';

const STATUS_COLORS = {
  Submitted: 'bg-[#EEEEFF] text-[#4131B0]',
  Approved: 'bg-[#E3F8E9] text-[#005F2E]',
  Rejected: 'bg-[#FFEBE8] text-[#B71824]',
  // Draft deliberately omitted - StatusBadge's own neutral default already matches.
};

// Quick-pick presets for the Task column - not linked to real Tasks from the Tasks module (things
// like "Standup Meeting" usually aren't formal Tasks anyway). "Other" reveals a free-text input
// instead of writing a literal "Other" into the entry - the presets themselves ARE the description,
// so no separate field is needed to track which preset is selected; it's derived from the current
// description value at render time (see the Task column below).
const TASK_PRESETS = ['Standup Meeting', 'Sprint Planning', 'Code Review', 'Client Call'];

const emptyEntry = () => ({ entryDate: '', entryTime: '', projectId: '', description: '', hours: '' });

// EntryDate is a full DateTime (same as ProjectTask.DueDate) - a time-of-day of exactly midnight
// means "no specific time was recorded", matching the same convention the Task meeting-time feature
// uses, not an entry that's deliberately timestamped at 00:00.
//
// UTC methods only, deliberately - every DateTime this API returns is labeled UTC by
// Infrastructure/Data/UtcDateTimeConverter regardless of what was actually sent (it never applies a
// real timezone conversion, just stamps Kind=Utc on the original wall-clock value), so getHours()/
// getMinutes() would silently reinterpret that label as a real UTC instant and shift the displayed
// time by the browser's local offset - getUTCHours()/getUTCMinutes() read back exactly what was sent.
const hasTimeComponent = (date) => date.getUTCHours() !== 0 || date.getUTCMinutes() !== 0;
const formatTime = (date) => {
  // Builds a throwaway Date with the UTC-extracted hours/minutes set as LOCAL ones, purely so
  // toLocaleTimeString can produce a locale-formatted string (e.g. "9:00 AM") without pulling the
  // browser's real UTC offset into the conversion at all.
  const relabeled = new Date();
  relabeled.setHours(date.getUTCHours(), date.getUTCMinutes(), 0, 0);
  return relabeled.toLocaleTimeString(undefined, { hour: 'numeric', minute: '2-digit' });
};

// Rendered by TimesheetsPage in place of the list (not a modal - filling in a detailed timesheet
// warranted the full page, matching how Wiki/Vault swap their list for a detail view). `timesheet`
// is either a full TimesheetDto (existing) or null (a brand-new one - PeriodStart/End were already
// collected by the caller before this opens, passed in via `initialPeriod`). `onClose` navigates back
// to the list rather than closing anything.
const TimesheetDetail = ({ apiService, user, timesheet, initialPeriod, projects, onClose, onChanged }) => {
  const toast = useToast();
  const isNew = !timesheet;
  const isOwn = isNew || timesheet.userId === user?.id;
  const canEdit = isOwn && (isNew || timesheet.status === 'Draft' || timesheet.status === 'Rejected');
  const canApprove = !isOwn && timesheet?.status === 'Submitted' && hasPermission(user?.permissions, 'timesheets.approve');

  const [entries, setEntries] = useState(
    (timesheet?.entries || []).map((e) => {
      const parsed = e.entryDate ? new Date(e.entryDate) : null;
      return {
        entryDate: (e.entryDate || '').slice(0, 10),
        entryTime: parsed && hasTimeComponent(parsed)
          ? `${String(parsed.getUTCHours()).padStart(2, '0')}:${String(parsed.getUTCMinutes()).padStart(2, '0')}`
          : '',
        projectId: e.projectId ? String(e.projectId) : '',
        // Kept alongside projectId purely for the read-only display below - the API already gives us
        // the name, so there's no need to cross-reference the `projects` list (which also wouldn't
        // reliably contain every project an entry could reference, e.g. an archived one).
        projectName: e.projectName || '',
        description: e.description || '',
        hours: String(e.hours)
      };
    }) || []
  );
  const [saving, setSaving] = useState(false);
  const [showSubmitPrompt, setShowSubmitPrompt] = useState(false);
  const [ccEmails, setCcEmails] = useState('');
  const [showRejectPrompt, setShowRejectPrompt] = useState(false);
  const [rejectReason, setRejectReason] = useState('');

  const totalHours = entries.reduce((sum, e) => sum + (Number(e.hours) || 0), 0);

  const updateEntry = (index, field, value) =>
    setEntries((prev) => prev.map((e, i) => (i === index ? { ...e, [field]: value } : e)));
  const addEntry = () => setEntries((prev) => [...prev, emptyEntry()]);
  const removeEntry = (index) => setEntries((prev) => prev.filter((_, i) => i !== index));

  const buildEntriesPayload = () => entries.map((e) => ({
    // Time is optional - an entry with none just keeps the date-only ("midnight") shape every entry
    // had before this feature existed.
    entryDate: e.entryTime ? `${e.entryDate}T${e.entryTime}:00` : e.entryDate,
    projectId: e.projectId ? Number(e.projectId) : null,
    description: e.description || null,
    hours: Number(e.hours)
  }));

  const handleSave = async () => {
    const validationErrors = validateTimesheet({ periodStart: timesheet?.periodStart || initialPeriod?.periodStart, periodEnd: timesheet?.periodEnd || initialPeriod?.periodEnd, entries });
    if (hasErrors(validationErrors)) {
      toast.error(Object.values(validationErrors)[0]);
      return;
    }
    setSaving(true);
    try {
      if (isNew) {
        await apiService.createTimesheet({ ...initialPeriod, entries: buildEntriesPayload() });
        toast.success('Timesheet created.');
      } else {
        await apiService.updateTimesheet(timesheet.id, { entries: buildEntriesPayload() });
        toast.success('Timesheet saved.');
      }
      await onChanged();
      onClose();
    } catch (err) {
      reportApiError(toast, err, `Could not ${isNew ? 'create' : 'save'} this timesheet.`);
    } finally {
      setSaving(false);
    }
  };

  const handleSubmit = async () => {
    setSaving(true);
    try {
      const cc = ccEmails.split(',').map((e) => e.trim()).filter(Boolean);
      await apiService.submitTimesheet(timesheet.id, cc);
      toast.success('Timesheet submitted.');
      await onChanged();
      onClose();
    } catch (err) {
      reportApiError(toast, err, 'Could not submit this timesheet.');
    } finally {
      setSaving(false);
    }
  };

  const handleApprove = async () => {
    setSaving(true);
    try {
      await apiService.approveTimesheet(timesheet.id);
      toast.success('Timesheet approved.');
      await onChanged();
      onClose();
    } catch (err) {
      reportApiError(toast, err, 'Could not approve this timesheet.');
    } finally {
      setSaving(false);
    }
  };

  const handleReject = async () => {
    if (!rejectReason.trim()) {
      toast.error('A rejection reason is required.');
      return;
    }
    setSaving(true);
    try {
      await apiService.rejectTimesheet(timesheet.id, rejectReason.trim());
      toast.success('Timesheet rejected.');
      await onChanged();
      onClose();
    } catch (err) {
      reportApiError(toast, err, 'Could not reject this timesheet.');
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex justify-between items-start gap-4">
        <div className="flex items-start gap-3">
          <button type="button" onClick={onClose} className="mt-0.5 text-gray-400 hover:text-gray-600 hover:bg-gray-100 p-1.5 rounded-md transition-colors" aria-label="Back to Timesheets">
            <ArrowLeft className="h-5 w-5" />
          </button>
          <div>
            <h2 className="text-[27px] font-bold text-gray-900 flex items-center gap-2">
              {isNew
                ? 'New Timesheet'
                : `${new Date(timesheet.periodStart).toLocaleDateString()} - ${new Date(timesheet.periodEnd).toLocaleDateString()}`}
              {!isNew && <StatusBadge status={timesheet.status} colorMap={STATUS_COLORS} />}
            </h2>
            {!isNew && !isOwn && <p className="text-gray-600">{timesheet.userName}</p>}
          </div>
        </div>
        {!isNew && (
          <button type="button" onClick={() => downloadTimesheetCsv(timesheet)} className="inline-flex items-center gap-2 bg-white text-gray-700 border border-gray-300 px-4 py-2.5 rounded-[10px] text-sm font-semibold hover:bg-gray-50 transition-colors flex-shrink-0">
            <Download className="h-4 w-4" /> Download
          </button>
        )}
      </div>

      {!isNew && timesheet.status === 'Rejected' && timesheet.rejectionReason && (
        <div className="bg-red-50 border border-red-200 rounded-lg p-3 text-sm text-red-700">
          <span className="font-semibold">Rejection reason: </span>{timesheet.rejectionReason}
        </div>
      )}
      {!isNew && timesheet.status === 'Approved' && (
        <p className="text-sm text-gray-500">Approved by {timesheet.approverName} on {new Date(timesheet.approvedAt).toLocaleDateString()}.</p>
      )}

      <div className="bg-white rounded-2xl border border-gray-100 shadow-sm overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead className="bg-gray-50/60 text-xs uppercase tracking-wide text-gray-500">
              <tr>
                <th className="text-left px-3 py-2 font-medium">Date</th>
                <th className="text-left px-3 py-2 font-medium">Project</th>
                <th className="text-left px-3 py-2 font-medium">Task</th>
                <th className="text-left px-3 py-2 font-medium">Time</th>
                <th className="text-left px-3 py-2 font-medium">Duration</th>
                {canEdit && <th className="w-8"></th>}
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {entries.length === 0 && (
                <tr><td colSpan={canEdit ? 6 : 5} className="px-3 py-6 text-center text-gray-400 italic">No entries yet.</td></tr>
              )}
              {entries.map((entry, i) => {
                const isPreset = TASK_PRESETS.includes(entry.description);
                const taskSelectValue = isPreset ? entry.description : (entry.description ? 'Other' : '');
                return (
                  <tr key={i}>
                    <td className="px-3 py-2 min-w-[9.5rem]">
                      {canEdit ? (
                        <input type="date" value={entry.entryDate} onChange={(e) => updateEntry(i, 'entryDate', e.target.value)}
                          className="w-full border border-gray-300 rounded-md px-2 py-1 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500" />
                      ) : new Date(entry.entryDate).toLocaleDateString()}
                    </td>
                    <td className="px-3 py-2 min-w-[9rem]">
                      {canEdit ? (
                        <select value={entry.projectId} onChange={(e) => updateEntry(i, 'projectId', e.target.value)}
                          className="w-full border border-gray-300 rounded-md px-2 py-1 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500">
                          <option value="">Non-billable</option>
                          {projects.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}
                        </select>
                      ) : (entry.projectId ? (entry.projectName || '—') : 'Non-billable')}
                    </td>
                    <td className="px-3 py-2 min-w-[10rem]">
                      {canEdit ? (
                        <div className="space-y-1">
                          <select
                            value={taskSelectValue}
                            onChange={(e) => updateEntry(i, 'description', e.target.value === 'Other' ? '' : e.target.value)}
                            className="w-full border border-gray-300 rounded-md px-2 py-1 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
                          >
                            <option value="">Select a task...</option>
                            {TASK_PRESETS.map((p) => <option key={p} value={p}>{p}</option>)}
                            <option value="Other">Other...</option>
                          </select>
                          {taskSelectValue === 'Other' && (
                            <input type="text" value={entry.description} onChange={(e) => updateEntry(i, 'description', e.target.value)}
                              placeholder="What did you work on?" autoFocus
                              className="w-full border border-gray-300 rounded-md px-2 py-1 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500" />
                          )}
                        </div>
                      ) : (entry.description || '—')}
                    </td>
                    <td className="px-3 py-2 min-w-[7rem]">
                      {canEdit ? (
                        <input type="time" value={entry.entryTime} onChange={(e) => updateEntry(i, 'entryTime', e.target.value)}
                          className="w-full border border-gray-300 rounded-md px-2 py-1 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500" />
                      ) : (entry.entryTime || (() => {
                        const d = new Date(entry.entryDate);
                        return hasTimeComponent(d) ? formatTime(d) : '—';
                      })())}
                    </td>
                    <td className="px-3 py-2 min-w-[8.5rem]">
                      {canEdit ? (() => {
                        const { wholeHours, minutes } = splitHours(entry.hours);
                        return (
                          <div className="flex items-center gap-1">
                            <input type="number" min="0" max="24" step="1" value={entry.hours ? wholeHours : ''}
                              onChange={(e) => updateEntry(i, 'hours', String(combineHours(e.target.value, minutes)))}
                              placeholder="0" aria-label="Hours"
                              className="w-14 border border-gray-300 rounded-md px-2 py-1 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500" />
                            <span className="text-gray-400 text-xs">h</span>
                            <input type="number" min="0" max="59" step="5" value={entry.hours ? minutes : ''}
                              onChange={(e) => updateEntry(i, 'hours', String(combineHours(wholeHours, e.target.value)))}
                              placeholder="0" aria-label="Minutes"
                              className="w-14 border border-gray-300 rounded-md px-2 py-1 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500" />
                            <span className="text-gray-400 text-xs">m</span>
                          </div>
                        );
                      })() : formatDuration(entry.hours)}
                    </td>
                    {canEdit && (
                      <td className="px-2 py-2">
                        <button type="button" onClick={() => removeEntry(i)} className="text-gray-400 hover:text-red-600 transition-colors" aria-label="Remove entry">
                          <Trash2 className="h-4 w-4" />
                        </button>
                      </td>
                    )}
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
        {canEdit && (
          <button type="button" onClick={addEntry} className="w-full flex items-center justify-center gap-1.5 py-2.5 text-sm font-medium text-blue-600 hover:bg-blue-50 border-t border-gray-100 transition-colors">
            <Plus className="h-4 w-4" /> Add entry
          </button>
        )}
      </div>

      <div className="flex justify-end items-baseline gap-2 text-sm text-gray-600 bg-gray-50 rounded-lg px-4 py-2.5">
        Total: <span className="text-lg font-semibold text-gray-900">{formatDuration(totalHours)}</span>
      </div>

      {showSubmitPrompt && (
        <div className="bg-gray-50 border border-gray-200 rounded-lg p-3 space-y-2">
          <label className="block text-sm text-gray-600" htmlFor="ts-cc-emails">CC anyone else (optional, e.g. your manager)</label>
          <input
            id="ts-cc-emails"
            type="text"
            value={ccEmails}
            onChange={(e) => setCcEmails(e.target.value)}
            placeholder="manager@company.com, another@company.com"
            className="w-full border border-gray-300 rounded-[10px] px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow"
          />
        </div>
      )}

      {showRejectPrompt && (
        <div className="bg-gray-50 border border-gray-200 rounded-lg p-3 space-y-2">
          <label className="block text-sm text-gray-600" htmlFor="ts-reject-reason">Rejection reason</label>
          <textarea
            id="ts-reject-reason"
            value={rejectReason}
            onChange={(e) => setRejectReason(e.target.value)}
            rows="2"
            className="w-full border border-gray-300 rounded-[10px] px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow"
          />
        </div>
      )}

      <div className="flex justify-end gap-3">
        <button type="button" onClick={onClose} disabled={saving} className="inline-flex items-center gap-2 bg-white text-gray-700 border border-gray-300 px-4 py-2.5 rounded-[10px] text-sm font-semibold hover:bg-gray-50 transition-colors disabled:opacity-50">
          {canEdit || canApprove ? 'Cancel' : 'Back'}
        </button>

        {canApprove && !showRejectPrompt && (
          <button type="button" onClick={() => setShowRejectPrompt(true)} disabled={saving} className="inline-flex items-center gap-2 bg-white text-red-600 border border-red-200 px-4 py-2.5 rounded-[10px] text-sm font-semibold hover:bg-red-50 transition-colors disabled:opacity-50">
            <Ban className="h-4 w-4" /> Reject
          </button>
        )}
        {canApprove && showRejectPrompt && (
          <button type="button" onClick={handleReject} disabled={saving} className="inline-flex items-center gap-2 bg-red-600 text-white px-4 py-2.5 rounded-[10px] text-sm font-semibold hover:bg-red-700 shadow-sm transition-colors disabled:opacity-50">
            {saving ? 'Rejecting...' : 'Confirm Reject'}
          </button>
        )}
        {canApprove && !showRejectPrompt && (
          <button type="button" onClick={handleApprove} disabled={saving} className="inline-flex items-center gap-2 bg-green-600 text-white px-4 py-2.5 rounded-[10px] text-sm font-semibold hover:bg-green-700 shadow-sm transition-colors disabled:opacity-50">
            <Check className="h-4 w-4" /> {saving ? 'Approving...' : 'Approve'}
          </button>
        )}

        {canEdit && (
          <button type="button" onClick={handleSave} disabled={saving} className="inline-flex items-center gap-2 bg-white text-blue-600 border border-blue-200 px-4 py-2.5 rounded-[10px] text-sm font-semibold hover:bg-blue-50 transition-colors disabled:opacity-50">
            {saving ? 'Saving...' : isNew ? 'Create Draft' : 'Save Draft'}
          </button>
        )}
        {canEdit && !isNew && !showSubmitPrompt && (
          <button type="button" onClick={() => setShowSubmitPrompt(true)} disabled={saving} className="inline-flex items-center gap-2 bg-blue-600 text-white px-4 py-2.5 rounded-[10px] text-sm font-semibold hover:bg-blue-700 shadow-sm transition-colors disabled:opacity-50">
            <Send className="h-4 w-4" /> Submit
          </button>
        )}
        {canEdit && !isNew && showSubmitPrompt && (
          <button type="button" onClick={handleSubmit} disabled={saving} className="inline-flex items-center gap-2 bg-blue-600 text-white px-4 py-2.5 rounded-[10px] text-sm font-semibold hover:bg-blue-700 shadow-sm transition-colors disabled:opacity-50">
            {saving ? 'Submitting...' : 'Confirm Submit'}
          </button>
        )}
      </div>
    </div>
  );
};

export default TimesheetDetail;
