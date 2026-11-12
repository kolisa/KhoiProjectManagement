// src/components/Reminders/ReminderForm.js
import React, { useState } from 'react';
import { X } from 'lucide-react';
import { validateReminder, hasErrors } from '../../utils/validation';
import useModalA11y from '../Common/useModalA11y';
import { Button, Input, Label, Select, Textarea } from '../ui';

const toLocalDateInput = (iso) => (iso ? new Date(iso).toISOString().slice(0, 10) : '');
const toLocalTimeInput = (iso) => (iso ? new Date(iso).toISOString().slice(11, 16) : '09:00');

const ReminderForm = ({ reminder, users, projects, canAssignOthers, onSave, onClose }) => {
  const modalRef = useModalA11y(onClose);
  const isEdit = !!reminder;
  const [title, setTitle] = useState(reminder?.title || '');
  const [description, setDescription] = useState(reminder?.description || '');
  const [dueDate, setDueDate] = useState(toLocalDateInput(reminder?.dueAt) || new Date().toISOString().slice(0, 10));
  const [dueTime, setDueTime] = useState(toLocalTimeInput(reminder?.dueAt));
  const [priority, setPriority] = useState(reminder?.priority || 'medium');
  const [category, setCategory] = useState(reminder?.category || '');
  const [assignedToId, setAssignedToId] = useState(reminder?.assignedToId || '');
  const [channel, setChannel] = useState(reminder?.channel || 'InApp');
  const [relatedProjectId, setRelatedProjectId] = useState(reminder?.relatedProjectId || '');
  const [showRecurrence, setShowRecurrence] = useState(!!reminder?.recurrenceType);
  const [recurrenceType, setRecurrenceType] = useState(reminder?.recurrenceType || 'Daily');
  const [recurrenceEndDate, setRecurrenceEndDate] = useState(toLocalDateInput(reminder?.recurrenceEndDate));
  const [recurrenceMaxOccurrences, setRecurrenceMaxOccurrences] = useState(reminder?.recurrenceMaxOccurrences || '');
  const [errors, setErrors] = useState({});
  const [apiError, setApiError] = useState(null);
  const [saving, setSaving] = useState(false);

  const buildDto = () => {
    const dueAt = new Date(`${dueDate}T${dueTime}:00`).toISOString();
    return {
      title: title.trim(),
      description: description.trim() || null,
      dueAt,
      priority,
      category: category.trim() || null,
      assignedToId: assignedToId ? Number(assignedToId) : null,
      channel,
      relatedProjectId: relatedProjectId ? Number(relatedProjectId) : null,
      recurrenceType: showRecurrence ? recurrenceType : null,
      recurrenceEndDate: showRecurrence && recurrenceEndDate ? new Date(recurrenceEndDate).toISOString() : null,
      recurrenceMaxOccurrences: showRecurrence && recurrenceMaxOccurrences ? Number(recurrenceMaxOccurrences) : null,
    };
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (saving) return; // guards against a double-click firing two submits

    const dto = buildDto();
    const validationErrors = validateReminder({ ...dto, dueAt: `${dueDate}T${dueTime}` });
    setErrors(validationErrors);
    if (hasErrors(validationErrors)) return;

    setSaving(true);
    setApiError(null);
    try {
      await onSave(dto);
    } catch (err) {
      setApiError(err.message);
      setSaving(false);
    }
  };

  return (
    <div className="fixed inset-0 bg-gray-900/50 backdrop-blur-xs flex items-center justify-center z-50 p-4">
      <form
        ref={modalRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby="reminder-form-modal-title"
        tabIndex={-1}
        onSubmit={handleSubmit}
        className="bg-white rounded-2xl shadow-xl w-full max-w-lg max-h-[90vh] overflow-hidden flex flex-col outline-hidden"
      >
        <div className="px-6 py-4 border-b border-gray-100 flex items-center justify-between shrink-0">
          <h3 id="reminder-form-modal-title" className="text-lg font-semibold text-gray-900">{isEdit ? 'Edit Reminder' : 'New Reminder'}</h3>
          <Button variant="ghost" size="sm" icon onClick={onClose} className="text-gray-400 hover:text-gray-600" aria-label="Close">
            <X className="h-4 w-4" />
          </Button>
        </div>

        <div className="px-6 py-5 space-y-3 overflow-y-auto">
        {apiError && <div className="text-red-600 text-sm mb-3">{apiError}</div>}

          <div>
            <Label htmlFor="reminder-title">Title</Label>
            <Input
              id="reminder-title"
              type="text"
              value={title}
              onChange={(e) => setTitle(e.target.value)}
              invalid={!!errors.title}
              aria-invalid={!!errors.title}
              aria-describedby={errors.title ? 'reminder-title-error' : undefined}
            />
            {errors.title && <p id="reminder-title-error" className="text-xs text-red-600 mt-1">{errors.title}</p>}
          </div>

          <div>
            <Label htmlFor="reminder-description">Description</Label>
            <Textarea
              id="reminder-description"
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              rows={3}
              invalid={!!errors.description}
            />
            {errors.description && <p className="text-xs text-red-600 mt-1">{errors.description}</p>}
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div>
              <Label htmlFor="reminder-due-date">Due date</Label>
              <Input
                id="reminder-due-date"
                type="date"
                value={dueDate}
                onChange={(e) => setDueDate(e.target.value)}
              />
            </div>
            <div>
              <Label htmlFor="reminder-due-time">Due time</Label>
              <Input
                id="reminder-due-time"
                type="time"
                value={dueTime}
                onChange={(e) => setDueTime(e.target.value)}
              />
            </div>
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div>
              <Label htmlFor="reminder-priority">Priority</Label>
              <Select
                id="reminder-priority"
                value={priority}
                onChange={(e) => setPriority(e.target.value)}
              >
                <option value="low">Low</option>
                <option value="medium">Medium</option>
                <option value="high">High</option>
              </Select>
            </div>
            <div>
              <Label htmlFor="reminder-category">Category</Label>
              <Input
                id="reminder-category"
                type="text"
                value={category}
                onChange={(e) => setCategory(e.target.value)}
                placeholder="e.g. Follow-up"
              />
            </div>
          </div>

          <div>
            <Label htmlFor="reminder-assigned">Assigned to</Label>
            <select
              id="reminder-assigned"
              value={assignedToId}
              onChange={(e) => setAssignedToId(e.target.value)}
              disabled={!canAssignOthers}
              className="disabled:bg-gray-100 disabled:text-gray-500"
            >
              <option value="">Myself</option>
              {canAssignOthers && users?.map((u) => (
                <option key={u.id} value={u.id}>{u.name}</option>
              ))}
            </select>
            {!canAssignOthers && (
              <p className="text-xs text-gray-400 mt-1">You can only create reminders for yourself.</p>
            )}
          </div>

          <div>
            <Label htmlFor="reminder-channel">Notify via</Label>
            <Select
              id="reminder-channel"
              value={channel}
              onChange={(e) => setChannel(e.target.value)}
            >
              <option value="InApp">In-app only</option>
              <option value="Email">Email only</option>
              <option value="Both">In-app and email</option>
            </Select>
          </div>

          {projects?.length > 0 && (
            <div>
              <Label htmlFor="reminder-related-project">Related project (optional)</Label>
              <Select
                id="reminder-related-project"
                value={relatedProjectId}
                onChange={(e) => setRelatedProjectId(e.target.value)}
              >
                <option value="">None</option>
                {projects.map((p) => (
                  <option key={p.id} value={p.id}>{p.name}</option>
                ))}
              </Select>
            </div>
          )}

          <div className="border-t pt-3">
            <label className="flex items-center text-sm text-gray-700">
              <input
                type="checkbox"
                checked={showRecurrence}
                onChange={(e) => setShowRecurrence(e.target.checked)}
                className="mr-2"
              />
              Repeat this reminder
            </label>

            {showRecurrence && (
              <div className="grid grid-cols-2 gap-3 mt-2">
                <div>
                  <label className="block text-xs text-gray-500 mb-1" htmlFor="reminder-recurrence-type">Repeats</label>
                  <select
                    id="reminder-recurrence-type"
                    value={recurrenceType}
                    onChange={(e) => setRecurrenceType(e.target.value)}
                    className="w-full border rounded-lg px-2 py-1.5 text-sm focus:outline-hidden focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow"
                  >
                    <option value="Daily">Daily</option>
                    <option value="Weekly">Weekly</option>
                    <option value="Monthly">Monthly</option>
                  </select>
                </div>
                <div>
                  <label className="block text-xs text-gray-500 mb-1" htmlFor="reminder-recurrence-end">Ends on (optional)</label>
                  <input
                    id="reminder-recurrence-end"
                    type="date"
                    value={recurrenceEndDate}
                    onChange={(e) => setRecurrenceEndDate(e.target.value)}
                    className="w-full border rounded-lg px-2 py-1.5 text-sm focus:outline-hidden focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow"
                  />
                </div>
                <div className="col-span-2">
                  <label className="block text-xs text-gray-500 mb-1" htmlFor="reminder-recurrence-max">Max occurrences (optional)</label>
                  <input
                    id="reminder-recurrence-max"
                    type="number"
                    min="1"
                    value={recurrenceMaxOccurrences}
                    onChange={(e) => setRecurrenceMaxOccurrences(e.target.value)}
                    className="w-full border rounded-lg px-2 py-1.5 text-sm focus:outline-hidden focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow"
                  />
                </div>
                {errors.recurrenceEndDate && (
                  <p className="col-span-2 text-xs text-red-600">{errors.recurrenceEndDate}</p>
                )}
              </div>
            )}
          </div>
        </div>

        <div className="px-6 py-4 border-t border-gray-100 flex justify-end gap-3 shrink-0">
          <Button variant="outline" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" disabled={saving}>
            {saving ? 'Saving...' : isEdit ? 'Save Changes' : 'Create Reminder'}
          </Button>
        </div>
      </form>
    </div>
  );
};

export default ReminderForm;
