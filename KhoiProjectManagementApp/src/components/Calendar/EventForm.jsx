// src/components/Calendar/EventForm.jsx
import React, { useState } from 'react';
import { X } from 'lucide-react';
import useModalA11y from '../Common/useModalA11y';
import { Button, Input, Label, Select, Textarea } from '../ui';

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
          <Button variant="ghost" size="sm" icon onClick={onClose} className="text-gray-400 hover:text-gray-600" aria-label="Close">
            <X className="h-4 w-4" />
          </Button>
        </div>

        <div className="px-6 py-5 space-y-3 overflow-y-auto">
          {apiError && <div className="text-red-600 text-sm mb-3">{apiError}</div>}

          <div>
            <Label htmlFor="event-title">Title</Label>
            <Input
              id="event-title"
              type="text"
              value={title}
              onChange={(e) => setTitle(e.target.value)}
              invalid={!!errors.title}
              aria-invalid={!!errors.title}
              aria-describedby={errors.title ? 'event-title-error' : undefined}
            />
            {errors.title && <p id="event-title-error" className="text-xs text-red-600 mt-1">{errors.title}</p>}
          </div>

          <div>
            <Label htmlFor="event-description">Description</Label>
            <Textarea
              id="event-description"
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              rows={3}
            />
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div>
              <Label htmlFor="event-date">Date</Label>
              <Input
                id="event-date"
                type="date"
                value={eventDate}
                onChange={(e) => setEventDate(e.target.value)}
              />
            </div>
            <div>
              <Label htmlFor="event-type">Type</Label>
              <Select
                id="event-type"
                value={eventType}
                onChange={(e) => setEventType(e.target.value)}
              >
                {EVENT_TYPES.map((t) => (
                  <option key={t} value={t}>{t}</option>
                ))}
              </Select>
            </div>
          </div>

          {eventType === 'Promotion' && (
            <div>
              <Label htmlFor="event-subject">Promoted</Label>
              <Select
                id="event-subject"
                value={subjectUserId}
                onChange={(e) => setSubjectUserId(e.target.value)}
                aria-invalid={undefined}
                className={errors.subjectUserId ? 'border-red-400' : ''}
              >
                <option value="">Select a person</option>
                {users?.map((u) => (
                  <option key={u.id} value={u.id}>{u.name}</option>
                ))}
              </Select>
              {errors.subjectUserId && <p className="text-xs text-red-600 mt-1">{errors.subjectUserId}</p>}
            </div>
          )}
        </div>

        <div className="px-6 py-4 border-t border-gray-100 flex justify-end gap-3 flex-shrink-0">
          <Button variant="outline" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" disabled={saving}>
            {saving ? 'Saving...' : isEdit ? 'Save Changes' : 'Create Event'}
          </Button>
        </div>
      </form>
    </div>
  );
};

export default EventForm;
