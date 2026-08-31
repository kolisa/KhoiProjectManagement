// src/components/Calendar/EventForm.jsx
import React, { useState } from 'react';
import { X } from 'lucide-react';
import useModalA11y from '../Common/useModalA11y';

const EVENT_TYPES = ['Event', 'Promotion', 'Marketing', 'Activation'];

const toLocalDateInput = (iso) => (iso ? new Date(iso).toISOString().slice(0, 10) : new Date().toISOString().slice(0, 10));

const EventForm = ({ event, users, onSave, onClose }) => {
  const modalRef = useModalA11y(onClose);
  const isEdit = !!event;
  const [title, setTitle] = useState(event?.title || '');
  const [description, setDescription] = useState(event?.description || '');
  const [eventDate, setEventDate] = useState(toLocalDateInput(event?.eventDate));
  const [eventType, setEventType] = useState(event?.eventType || 'Event');
  const [subjectUserId, setSubjectUserId] = useState(event?.subjectUserId || '');
  const [errors, setErrors] = useState({});
  const [apiError, setApiError] = useState(null);
  const [saving, setSaving] = useState(false);

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (saving) return; // guards against a double-click firing two submits

    const validationErrors = {};
    if (!title.trim()) validationErrors.title = 'Title is required.';
    if (eventType === 'Promotion' && !subjectUserId) validationErrors.subjectUserId = 'Select who was promoted.';
    setErrors(validationErrors);
    if (Object.keys(validationErrors).length > 0) return;

    const dto = {
      title: title.trim(),
      description: description.trim() || null,
      eventDate: new Date(`${eventDate}T00:00:00`).toISOString(),
      eventType,
      subjectUserId: eventType === 'Promotion' && subjectUserId ? Number(subjectUserId) : null,
    };

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
    <div className="fixed inset-0 bg-gray-900/50 backdrop-blur-sm flex items-center justify-center z-50 p-4">
      <form
        ref={modalRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby="event-form-modal-title"
        tabIndex={-1}
        onSubmit={handleSubmit}
        className="bg-white rounded-2xl shadow-xl w-full max-w-lg max-h-[90vh] overflow-hidden flex flex-col outline-none"
      >
        <div className="px-6 py-4 border-b border-gray-100 flex items-center justify-between flex-shrink-0">
          <h3 id="event-form-modal-title" className="text-lg font-semibold text-gray-900">{isEdit ? 'Edit Event' : 'New Event'}</h3>
          <button type="button" onClick={onClose} className="text-gray-400 hover:text-gray-600 hover:bg-gray-100 p-1 rounded-md transition-colors" aria-label="Close">
            <X className="h-4 w-4" />
          </button>
        </div>

        <div className="px-6 py-5 space-y-3 overflow-y-auto">
          {apiError && <div className="text-red-600 text-sm mb-3">{apiError}</div>}

          <div>
            <label className="block text-sm text-gray-600 mb-1" htmlFor="event-title">Title</label>
            <input
              id="event-title"
              type="text"
              value={title}
              onChange={(e) => setTitle(e.target.value)}
              className={`w-full border rounded-lg px-3 py-2 ${errors.title ? 'border-red-400' : ''} focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow`}
              aria-invalid={!!errors.title}
              aria-describedby={errors.title ? 'event-title-error' : undefined}
            />
            {errors.title && <p id="event-title-error" className="text-xs text-red-600 mt-1">{errors.title}</p>}
          </div>

          <div>
            <label className="block text-sm text-gray-600 mb-1" htmlFor="event-description">Description</label>
            <textarea
              id="event-description"
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              rows={3}
              className="w-full border rounded-lg px-3 py-2 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow"
            />
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-sm text-gray-600 mb-1" htmlFor="event-date">Date</label>
              <input
                id="event-date"
                type="date"
                value={eventDate}
                onChange={(e) => setEventDate(e.target.value)}
                className="w-full border rounded-lg px-3 py-2 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow"
              />
            </div>
            <div>
              <label className="block text-sm text-gray-600 mb-1" htmlFor="event-type">Type</label>
              <select
                id="event-type"
                value={eventType}
                onChange={(e) => setEventType(e.target.value)}
                className="w-full border rounded-lg px-3 py-2 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow"
              >
                {EVENT_TYPES.map((t) => (
                  <option key={t} value={t}>{t}</option>
                ))}
              </select>
            </div>
          </div>

          {eventType === 'Promotion' && (
            <div>
              <label className="block text-sm text-gray-600 mb-1" htmlFor="event-subject">Promoted</label>
              <select
                id="event-subject"
                value={subjectUserId}
                onChange={(e) => setSubjectUserId(e.target.value)}
                className={`w-full border rounded-lg px-3 py-2 ${errors.subjectUserId ? 'border-red-400' : ''} focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow`}
              >
                <option value="">Select a person</option>
                {users?.map((u) => (
                  <option key={u.id} value={u.id}>{u.name}</option>
                ))}
              </select>
              {errors.subjectUserId && <p className="text-xs text-red-600 mt-1">{errors.subjectUserId}</p>}
            </div>
          )}
        </div>

        <div className="px-6 py-4 border-t border-gray-100 flex justify-end gap-3 flex-shrink-0">
          <button type="button" onClick={onClose} className="inline-flex items-center gap-2 bg-white text-gray-700 border border-gray-300 px-4 py-2.5 rounded-[10px] text-sm font-semibold hover:bg-gray-50 transition-colors">
            Cancel
          </button>
          <button
            type="submit"
            disabled={saving}
            className="inline-flex items-center gap-2 bg-blue-600 text-white px-4 py-2.5 rounded-[10px] text-sm font-semibold hover:bg-blue-700 shadow-sm transition-colors disabled:opacity-50"
          >
            {saving ? 'Saving...' : isEdit ? 'Save Changes' : 'Create Event'}
          </button>
        </div>
      </form>
    </div>
  );
};

export default EventForm;
