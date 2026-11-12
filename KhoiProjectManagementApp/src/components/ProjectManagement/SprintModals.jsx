import React, { useState } from 'react';
import { X } from 'lucide-react';
import { Button, Modal } from '../ui';

const fieldClass = 'w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-hidden focus:ring-2 focus:ring-blue-500';
const labelClass = 'block text-sm font-medium text-gray-700 mb-1';

const Shell = ({ title, titleId, onClose, children }) => (
  <Modal onClose={onClose} size="md" closeOnBackdrop={false} padded={false} titleId={titleId}
    header={(
      <div className="flex items-center justify-between px-5 py-4 border-b border-gray-200">
        <h2 id={titleId} className="text-lg font-semibold text-gray-900">{title}</h2>
        <button onClick={onClose} aria-label="Close" className="text-gray-400 hover:text-gray-600"><X className="h-5 w-5" /></button>
      </div>
    )}>
    {children}
  </Modal>
);

const toDateInput = (d) => (d ? new Date(d).toISOString().slice(0, 10) : '');

export const SprintModal = ({ sprint, onSave, onClose }) => {
  const today = new Date();
  const defaultEnd = new Date(today.getTime() + 13 * 86400000);
  const [form, setForm] = useState({
    name: sprint?.name || '',
    goal: sprint?.goal || '',
    startDate: toDateInput(sprint?.startDate || today),
    endDate: toDateInput(sprint?.endDate || defaultEnd),
    capacity: sprint?.capacity ?? '',
  });
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState(null);
  const set = (k) => (e) => setForm((f) => ({ ...f, [k]: e.target.value }));

  const submit = async (e) => {
    e.preventDefault();
    if (!form.name.trim()) { setError('A sprint needs a name.'); return; }
    if (!form.startDate || !form.endDate || form.endDate <= form.startDate) { setError('The end date must be after the start date.'); return; }
    setSaving(true);
    setError(null);
    try {
      await onSave({
        name: form.name.trim(),
        goal: form.goal,
        startDate: form.startDate,
        endDate: form.endDate,
        capacity: form.capacity === '' ? null : Number(form.capacity),
      });
    } catch (err) {
      setError(err?.message || 'Could not save the sprint.');
      setSaving(false);
    }
  };

  return (
    <Shell title={sprint ? 'Edit sprint' : 'New sprint'} titleId="sprint-modal-title" onClose={onClose}>
      <form onSubmit={submit} className="p-5 space-y-4">
        {error && <div role="alert" className="text-sm text-red-700 bg-red-50 border border-red-200 rounded-lg px-3 py-2">{error}</div>}
        <div>
          <label htmlFor="sp-name" className={labelClass}>Name</label>
          <input id="sp-name" className={fieldClass} value={form.name} onChange={set('name')} maxLength={100} placeholder="Sprint 1" />
        </div>
        <div>
          <label htmlFor="sp-goal" className={labelClass}>Sprint goal</label>
          <textarea id="sp-goal" rows={3} className={fieldClass} value={form.goal} onChange={set('goal')} maxLength={500} />
        </div>
        <div className="grid grid-cols-2 gap-4">
          <div>
            <label htmlFor="sp-start" className={labelClass}>Start</label>
            <input id="sp-start" type="date" className={fieldClass} value={form.startDate} onChange={set('startDate')} />
          </div>
          <div>
            <label htmlFor="sp-end" className={labelClass}>End</label>
            <input id="sp-end" type="date" className={fieldClass} value={form.endDate} onChange={set('endDate')} />
          </div>
        </div>
        <div>
          <label htmlFor="sp-cap" className={labelClass}>Capacity (story points)</label>
          <input id="sp-cap" type="number" min="0" max="1000" className={fieldClass} value={form.capacity} onChange={set('capacity')} placeholder="Optional" />
        </div>
        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="outline" onClick={onClose}>Cancel</Button>
          <Button type="submit" disabled={saving}>{saving ? 'Saving…' : 'Save'}</Button>
        </div>
      </form>
    </Shell>
  );
};

export const CompleteSprintModal = ({ sprint, unfinishedCount, plannedSprints, onComplete, onClose }) => {
  const [target, setTarget] = useState('');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState(null);

  const submit = async (e) => {
    e.preventDefault();
    setSaving(true);
    setError(null);
    try {
      await onComplete(target === '' ? null : Number(target));
    } catch (err) {
      setError(err?.message || 'Could not complete the sprint.');
      setSaving(false);
    }
  };

  return (
    <Shell title={`Complete ${sprint.name}`} titleId="complete-sprint-title" onClose={onClose}>
      <form onSubmit={submit} className="p-5 space-y-4">
        {error && <div role="alert" className="text-sm text-red-700 bg-red-50 border border-red-200 rounded-lg px-3 py-2">{error}</div>}
        <p className="text-sm text-gray-700">
          {sprint.donePoints} of {sprint.totalPoints} points are done.{' '}
          {unfinishedCount > 0
            ? `${unfinishedCount} unfinished item${unfinishedCount === 1 ? '' : 's'} will be moved:`
            : 'Everything is finished.'}
        </p>
        {unfinishedCount > 0 && (
          <div>
            <label htmlFor="cs-target" className={labelClass}>Move unfinished items to</label>
            <select id="cs-target" className={fieldClass} value={target} onChange={(e) => setTarget(e.target.value)}>
              <option value="">Backlog</option>
              {plannedSprints.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
            </select>
          </div>
        )}
        <p className="text-xs text-gray-500">Completing locks the sprint's velocity. This cannot be undone.</p>
        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="outline" onClick={onClose}>Cancel</Button>
          <Button type="submit" disabled={saving}>{saving ? 'Completing…' : 'Complete sprint'}</Button>
        </div>
      </form>
    </Shell>
  );
};
