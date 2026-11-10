import React, { useState } from 'react';
import { X } from 'lucide-react';
import useModalA11y from '../Common/useModalA11y';
import { WORK_ITEM_TYPES, STATUSES, allowedParentTypes, can } from './pmConstants';
import WorkItemCollab from './WorkItemCollab';

const fieldClass = 'w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500';
const labelClass = 'block text-sm font-medium text-gray-700 mb-1';

// Create/edit a backlog work item. `item` is null for create. `items` is the project's current
// backlog (for the parent picker); `members` feeds the assignee picker.
const WorkItemModal = ({ item, items, members, canEdit, onSave, onClose, apiService, role, onChanged }) => {
  const modalRef = useModalA11y(onClose);
  const [form, setForm] = useState({
    title: item?.title || '',
    description: item?.description || '',
    type: item?.type || 'Story',
    status: item?.status || 'todo',
    priority: item?.priority || 'medium',
    storyPoints: item?.storyPoints ?? '',
    assignedToId: item?.assignedToId ?? '',
    parentId: item?.parentId ?? '',
    // An item created without a due date carries a placeholder date server-side; show the field empty, not that date.
    dueDate: item?.dueDate && item.hasDueDate ? item.dueDate.slice(0, 10) : '',
  });
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState(null);

  const set = (key) => (e) => setForm((f) => ({ ...f, [key]: e.target.value }));

  // Changing type clears a parent that would no longer be legal rather than silently submitting it.
  const changeType = (e) => {
    const type = e.target.value;
    const parent = items.find((i) => i.id === Number(form.parentId));
    const stillValid = !parent || allowedParentTypes(type).includes(parent.type);
    setForm((f) => ({ ...f, type, parentId: stillValid ? f.parentId : '' }));
  };

  const parentOptions = items.filter(
    (i) => i.id !== item?.id && allowedParentTypes(form.type).includes(i.type)
  );

  const submit = async (e) => {
    e.preventDefault();
    if (!form.title.trim()) {
      setError('Title is required.');
      return;
    }
    if (form.type === 'Subtask' && !form.parentId) {
      setError('A Subtask must have a parent item.');
      return;
    }
    setSaving(true);
    setError(null);
    try {
      await onSave({
        title: form.title.trim(),
        description: form.description,
        type: form.type,
        status: form.status,
        priority: form.priority,
        storyPoints: form.storyPoints === '' ? null : Number(form.storyPoints),
        assignedToId: form.assignedToId === '' ? null : Number(form.assignedToId),
        parentId: form.parentId === '' ? null : Number(form.parentId),
        dueDate: form.dueDate ? new Date(form.dueDate).toISOString() : null,
      });
    } catch (err) {
      setError(err?.message || 'Could not save the work item.');
      setSaving(false);
    }
  };

  const disabled = !canEdit;

  return (
    <div className="fixed inset-0 bg-black/40 z-50 flex items-center justify-center p-4">
      <div
        ref={modalRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby="work-item-modal-title"
        tabIndex={-1}
        className="bg-white rounded-xl shadow-xl w-full max-w-2xl max-h-[90vh] overflow-y-auto"
      >
        <div className="flex items-center justify-between px-5 py-4 border-b border-gray-200">
          <h2 id="work-item-modal-title" className="text-lg font-semibold text-gray-900">
            {item ? `Work item #${item.id}` : 'New work item'}
          </h2>
          <button onClick={onClose} aria-label="Close" className="text-gray-400 hover:text-gray-600">
            <X className="h-5 w-5" />
          </button>
        </div>

        <form onSubmit={submit} className="p-5 space-y-4">
          {error && <div role="alert" className="text-sm text-red-700 bg-red-50 border border-red-200 rounded-lg px-3 py-2">{error}</div>}

          <div>
            <label htmlFor="wi-title" className={labelClass}>Title</label>
            <input id="wi-title" className={fieldClass} value={form.title} onChange={set('title')} maxLength={200} disabled={disabled} />
          </div>

          <div>
            <label htmlFor="wi-desc" className={labelClass}>Description</label>
            <textarea id="wi-desc" rows={4} className={fieldClass} value={form.description} onChange={set('description')} maxLength={4000} disabled={disabled} />
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div>
              <label htmlFor="wi-type" className={labelClass}>Type</label>
              <select id="wi-type" className={fieldClass} value={form.type} onChange={changeType} disabled={disabled}>
                {WORK_ITEM_TYPES.map((t) => <option key={t}>{t}</option>)}
                {/* Legacy task types stay visible so editing one doesn't silently retype it. */}
                {item && !WORK_ITEM_TYPES.includes(item.type) && <option>{item.type}</option>}
              </select>
            </div>
            <div>
              <label htmlFor="wi-status" className={labelClass}>Status</label>
              <select id="wi-status" className={fieldClass} value={form.status} onChange={set('status')} disabled={disabled}>
                {STATUSES.map((s) => <option key={s.key} value={s.key}>{s.label}</option>)}
              </select>
            </div>
            <div>
              <label htmlFor="wi-priority" className={labelClass}>Priority</label>
              <select id="wi-priority" className={fieldClass} value={form.priority} onChange={set('priority')} disabled={disabled}>
                <option value="low">Low</option>
                <option value="medium">Medium</option>
                <option value="high">High</option>
              </select>
            </div>
            <div>
              <label htmlFor="wi-points" className={labelClass}>Story points</label>
              <input id="wi-points" type="number" min="0" max="100" className={fieldClass} value={form.storyPoints} onChange={set('storyPoints')} disabled={disabled} />
            </div>
            <div>
              <label htmlFor="wi-assignee" className={labelClass}>Assignee</label>
              <select id="wi-assignee" className={fieldClass} value={form.assignedToId} onChange={set('assignedToId')} disabled={disabled}>
                <option value="">Unassigned</option>
                {members.map((m) => <option key={m.userId} value={m.userId}>{m.name}</option>)}
              </select>
            </div>
            <div>
              <label htmlFor="wi-due" className={labelClass}>Due date</label>
              <input id="wi-due" type="date" className={fieldClass} value={form.dueDate} onChange={set('dueDate')} disabled={disabled} />
            </div>
          </div>

          <div>
            <label htmlFor="wi-parent" className={labelClass}>Parent</label>
            <select id="wi-parent" className={fieldClass} value={form.parentId} onChange={set('parentId')} disabled={disabled || form.type === 'Epic'}>
              <option value="">{form.type === 'Subtask' ? 'Select a parent…' : 'None'}</option>
              {parentOptions.map((p) => <option key={p.id} value={p.id}>{p.type}: {p.title}</option>)}
            </select>
          </div>

          <div className="flex justify-end gap-2 pt-2">
            <button type="button" onClick={onClose} className="px-4 py-2 text-sm rounded-lg border border-gray-300 text-gray-700 hover:bg-gray-50">
              {canEdit ? 'Cancel' : 'Close'}
            </button>
            {canEdit && (
              <button type="submit" disabled={saving} className="px-4 py-2 text-sm rounded-lg bg-blue-600 text-white font-medium hover:bg-blue-700 disabled:opacity-60">
                {saving ? 'Saving…' : 'Save'}
              </button>
            )}
          </div>
        </form>

        {item && apiService && (
          <WorkItemCollab
            apiService={apiService}
            item={item}
            items={items}
            members={members}
            can={{ collaborate: can(role, 'collaborate'), edit: can(role, 'edit') }}
            onChanged={onChanged}
          />
        )}
      </div>
    </div>
  );
};

export default WorkItemModal;
