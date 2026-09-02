// src/components/Timesheets/TimesheetDetail.jsx
import React, { useState } from 'react';
import { X, Plus, Trash2, Send, Check, Ban } from 'lucide-react';
import { hasPermission } from '../../utils/permissions';
import { validateTimesheet, hasErrors } from '../../utils/validation';
import { useToast } from '../../contexts/ToastContext';
import { reportApiError } from '../../utils/apiError';
import useModalA11y from '../Common/useModalA11y';
import StatusBadge from '../Common/StatusBadge';

const STATUS_COLORS = {
  Submitted: 'bg-[#EEEEFF] text-[#4131B0]',
  Approved: 'bg-[#E3F8E9] text-[#005F2E]',
  Rejected: 'bg-[#FFEBE8] text-[#B71824]',
  // Draft deliberately omitted - StatusBadge's own neutral default already matches.
};

const emptyEntry = () => ({ entryDate: '', projectId: '', description: '', hours: '' });

// One modal handles create-and-edit like the Project/Task modals elsewhere in this app: `timesheet`
// is either a full TimesheetDto (existing) or null (a brand-new one - PeriodStart/End were already
// collected by the caller before this opens, passed in via `initialPeriod`).
const TimesheetDetail = ({ apiService, user, timesheet, initialPeriod, projects, onClose, onChanged }) => {
  const toast = useToast();
  const modalRef = useModalA11y(onClose);
  const isNew = !timesheet;
  const isOwn = isNew || timesheet.userId === user?.id;
  const canEdit = isOwn && (isNew || timesheet.status === 'Draft' || timesheet.status === 'Rejected');
  const canApprove = !isOwn && timesheet?.status === 'Submitted' && hasPermission(user?.permissions, 'timesheets.approve');

  const [entries, setEntries] = useState(
    (timesheet?.entries || []).map((e) => ({
      entryDate: (e.entryDate || '').slice(0, 10),
      projectId: e.projectId ? String(e.projectId) : '',
      // Kept alongside projectId purely for the read-only display below - the API already gives us
      // the name, so there's no need to cross-reference the `projects` list (which also wouldn't
      // reliably contain every project an entry could reference, e.g. an archived one).
      projectName: e.projectName || '',
      description: e.description || '',
      hours: String(e.hours)
    })) || []
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
    entryDate: e.entryDate,
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
    <div className="fixed inset-0 bg-gray-900/50 backdrop-blur-sm flex items-center justify-center p-4 z-50">
      <div ref={modalRef} role="dialog" aria-modal="true" aria-labelledby="timesheet-modal-title" tabIndex={-1} className="bg-white rounded-2xl shadow-xl max-w-2xl w-full max-h-[90vh] overflow-y-auto outline-none">
        <div className="px-6 py-4 border-b border-gray-100 flex items-center justify-between flex-shrink-0">
          <div>
            <h3 id="timesheet-modal-title" className="text-base font-semibold text-gray-900 flex items-center gap-2">
              {isNew
                ? 'New Timesheet'
                : `${new Date(timesheet.periodStart).toLocaleDateString()} - ${new Date(timesheet.periodEnd).toLocaleDateString()}`}
              {!isNew && <StatusBadge status={timesheet.status} colorMap={STATUS_COLORS} />}
            </h3>
            {!isNew && !isOwn && <p className="text-xs text-gray-500 mt-0.5">{timesheet.userName}</p>}
          </div>
          <button type="button" onClick={onClose} className="text-gray-400 hover:text-gray-600 hover:bg-gray-100 p-1 rounded-md transition-colors" aria-label="Close">
            <X className="h-4 w-4" />
          </button>
        </div>

        <div className="px-6 py-5 space-y-4">
          {!isNew && timesheet.status === 'Rejected' && timesheet.rejectionReason && (
            <div className="bg-red-50 border border-red-200 rounded-lg p-3 text-sm text-red-700">
              <span className="font-semibold">Rejection reason: </span>{timesheet.rejectionReason}
            </div>
          )}
          {!isNew && timesheet.status === 'Approved' && (
            <p className="text-sm text-gray-500">Approved by {timesheet.approverName} on {new Date(timesheet.approvedAt).toLocaleDateString()}.</p>
          )}

          <div className="border border-gray-100 rounded-2xl overflow-hidden">
            <table className="w-full text-sm">
              <thead className="bg-gray-50/60 text-xs uppercase tracking-wide text-gray-500">
                <tr>
                  <th className="text-left px-3 py-2 font-medium">Date</th>
                  <th className="text-left px-3 py-2 font-medium">Project</th>
                  <th className="text-left px-3 py-2 font-medium">Description</th>
                  <th className="text-left px-3 py-2 font-medium">Hours</th>
                  {canEdit && <th className="w-8"></th>}
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {entries.length === 0 && (
                  <tr><td colSpan={canEdit ? 5 : 4} className="px-3 py-6 text-center text-gray-400 italic">No entries yet.</td></tr>
                )}
                {entries.map((entry, i) => (
                  <tr key={i}>
                    <td className="px-3 py-2">
                      {canEdit ? (
                        <input type="date" value={entry.entryDate} onChange={(e) => updateEntry(i, 'entryDate', e.target.value)}
                          className="w-full border border-gray-300 rounded-md px-2 py-1 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500" />
                      ) : new Date(entry.entryDate).toLocaleDateString()}
                    </td>
                    <td className="px-3 py-2">
                      {canEdit ? (
                        <select value={entry.projectId} onChange={(e) => updateEntry(i, 'projectId', e.target.value)}
                          className="w-full border border-gray-300 rounded-md px-2 py-1 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500">
                          <option value="">Non-billable</option>
                          {projects.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}
                        </select>
                      ) : (entry.projectId ? (entry.projectName || '—') : 'Non-billable')}
                    </td>
                    <td className="px-3 py-2">
                      {canEdit ? (
                        <input type="text" value={entry.description} onChange={(e) => updateEntry(i, 'description', e.target.value)}
                          placeholder="What did you work on?"
                          className="w-full border border-gray-300 rounded-md px-2 py-1 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500" />
                      ) : (entry.description || '—')}
                    </td>
                    <td className="px-3 py-2 w-24">
                      {canEdit ? (
                        <input type="number" min="0.25" max="24" step="0.25" value={entry.hours} onChange={(e) => updateEntry(i, 'hours', e.target.value)}
                          className="w-full border border-gray-300 rounded-md px-2 py-1 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500" />
                      ) : entry.hours}
                    </td>
                    {canEdit && (
                      <td className="px-2 py-2">
                        <button type="button" onClick={() => removeEntry(i)} className="text-gray-400 hover:text-red-600 transition-colors" aria-label="Remove entry">
                          <Trash2 className="h-4 w-4" />
                        </button>
                      </td>
                    )}
                  </tr>
                ))}
              </tbody>
            </table>
            {canEdit && (
              <button type="button" onClick={addEntry} className="w-full flex items-center justify-center gap-1.5 py-2.5 text-sm font-medium text-blue-600 hover:bg-blue-50 border-t border-gray-100 transition-colors">
                <Plus className="h-4 w-4" /> Add entry
              </button>
            )}
          </div>

          <div className="flex justify-end text-sm text-gray-600">
            Total: <span className="font-semibold text-gray-900 ml-1">{totalHours}h</span>
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
        </div>

        <div className="px-6 py-4 border-t border-gray-100 flex justify-end gap-3">
          <button type="button" onClick={onClose} disabled={saving} className="inline-flex items-center gap-2 bg-white text-gray-700 border border-gray-300 px-4 py-2.5 rounded-[10px] text-sm font-semibold hover:bg-gray-50 transition-colors disabled:opacity-50">
            {canEdit || canApprove ? 'Cancel' : 'Close'}
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
              {saving ? 'Saving...' : isNew ? 'Create' : 'Save'}
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
    </div>
  );
};

export default TimesheetDetail;
