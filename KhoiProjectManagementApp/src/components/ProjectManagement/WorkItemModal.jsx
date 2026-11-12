import React, { useState } from 'react';
import { X } from 'lucide-react';
import { Button } from '../ui';
import useModalA11y from '../Common/useModalA11y';
import { WORK_ITEM_TYPES, STATUSES, TYPE_STYLES, allowedParentTypes, can, denyReason } from './pmConstants';
import WorkItemCollab from './WorkItemCollab';
import RichTextEditor from './RichTextEditor';
import Combobox, { peopleOptions } from './Combobox';
import SubtasksPanel from './SubtasksPanel';
import EffortEditor from './EffortEditor';
import { isEmptyHtml } from './richText';

const fieldClass = 'w-full border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-hidden focus:ring-2 focus:ring-blue-500 disabled:bg-gray-50 disabled:text-gray-500 disabled:cursor-not-allowed';
const labelClass = 'block text-xs font-medium text-gray-600 mb-1';

// Create/edit a backlog work item. `item` is null for create. `items` is the project's current backlog (for the parent
// picker and the subtask list); `members` feeds the assignee/effort pickers.
// Layout: the work itself (title, rich-text description, subtasks, effort) on the left, the item's details on the right,
// the discussion below.
const WorkItemModal = ({ item, items, members, canEdit, onSave, onClose, apiService, role, project, onChanged }) => {
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
  const [effortDirty, setEffortDirty] = useState(false);

  const set = (key) => (e) => setForm((f) => ({ ...f, [key]: e.target.value }));
  const setDescription = (html) => setForm((f) => ({ ...f, description: html }));

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
        description: isEmptyHtml(form.description) ? '' : form.description,
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
  const editReason = denyReason(project, item ? 'edit' : 'create');
  const typeStyle = TYPE_STYLES[form.type] || 'bg-gray-100 text-gray-700';

  return (
    <div className="fixed inset-0 bg-black/40 z-50 flex items-center justify-center p-4">
      <div
        ref={modalRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby="work-item-modal-title"
        tabIndex={-1}
        className="bg-white rounded-xl shadow-xl w-full max-w-5xl max-h-[92vh] flex flex-col outline-hidden"
      >
        <div className="flex items-center gap-3 px-6 py-4 border-b border-gray-200">
          <span className={`px-2 py-0.5 rounded-sm text-[11px] font-semibold ${typeStyle}`}>{form.type}</span>
          <h2 id="work-item-modal-title" className="text-lg font-semibold text-gray-900">
            {item ? `Work item #${item.id}` : 'New work item'}
          </h2>
          <button onClick={onClose} aria-label="Close" className="ml-auto text-gray-400 hover:text-gray-600">
            <X className="h-5 w-5" />
          </button>
        </div>

        <div className="flex-1 overflow-y-auto">
        <form id="work-item-form" onSubmit={submit} noValidate>
          <div className="grid gap-6 p-6 lg:grid-cols-[minmax(0,1fr)_17rem]">
            <div className="space-y-5 min-w-0">
              {error && <div role="alert" className="text-sm text-red-700 bg-red-50 border border-red-200 rounded-lg px-3 py-2">{error}</div>}

              <div>
                <label htmlFor="wi-title" className={labelClass}>Title</label>
                <input id="wi-title" className={`${fieldClass} text-base font-medium`} value={form.title} onChange={set('title')} maxLength={200} disabled={disabled} title={disabled ? editReason || undefined : undefined} />
              </div>

              <RichTextEditor id="wi-desc" label="Description" value={item?.description || ''} onChange={setDescription} disabled={disabled} reason={editReason} />

              <SubtasksPanel apiService={apiService} project={project} item={item} items={items} members={members} description={form.description}
                canCreate={can(role, 'create')} reason={denyReason(project, 'create')} onChanged={onChanged} />

              {item && apiService && (
                <EffortEditor apiService={apiService} item={item} members={members} canEdit={can(role, 'edit')} reason={denyReason(project, 'edit')} onSaved={onChanged} onDirtyChange={setEffortDirty} />
              )}
            </div>

            <aside aria-label="Details" className="space-y-3 lg:border-l lg:border-gray-100 lg:pl-6">
              <h3 className="text-xs font-semibold uppercase tracking-wide text-gray-500">Details</h3>
              <div>
                <label htmlFor="wi-status" className={labelClass}>Status</label>
                <select id="wi-status" className={fieldClass} value={form.status} onChange={set('status')} disabled={disabled}>
                  {STATUSES.map((s) => <option key={s.key} value={s.key}>{s.label}</option>)}
                </select>
              </div>
              <div>
                <label htmlFor="wi-type" className={labelClass}>Type</label>
                <select id="wi-type" className={fieldClass} value={form.type} onChange={changeType} disabled={disabled}>
                  {WORK_ITEM_TYPES.map((t) => <option key={t}>{t}</option>)}
                  {/* Legacy task types stay visible so editing one doesn't silently retype it. */}
                  {item && !WORK_ITEM_TYPES.includes(item.type) && <option>{item.type}</option>}
                </select>
              </div>
              <div>
                <label htmlFor="wi-assignee" className={labelClass}>Assignee</label>
                <Combobox id="wi-assignee" value={form.assignedToId} onChange={(v) => setForm((f) => ({ ...f, assignedToId: v }))}
                  options={peopleOptions(members)} emptyLabel="Unassigned" disabled={disabled} searchPlaceholder="Search people…" />
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
                <label htmlFor="wi-due" className={labelClass}>Due date</label>
                <input id="wi-due" type="date" className={fieldClass} value={form.dueDate} onChange={set('dueDate')} disabled={disabled} />
              </div>
              <div>
                <label htmlFor="wi-parent" className={labelClass}>Parent</label>
                <Combobox id="wi-parent" value={form.parentId} onChange={(v) => setForm((f) => ({ ...f, parentId: v }))}
                  options={parentOptions.map((p) => ({ value: p.id, label: p.title, description: `${p.type} #${p.id}` }))}
                  emptyLabel={form.type === 'Subtask' ? 'Select a parent…' : 'None'} disabled={disabled || form.type === 'Epic'} searchPlaceholder="Search items…" />
              </div>
              <div>
                <label htmlFor="wi-points" className={labelClass}>Story points</label>
                <input id="wi-points" type="number" min="0" max="100" className={fieldClass} value={form.storyPoints} onChange={set('storyPoints')} disabled={disabled} />
                <p className="mt-1 text-[11px] text-gray-500">A single estimate for the item. Points per person are set under Effort.</p>
              </div>
            </aside>
          </div>

        </form>

        {item && apiService && (
          <div className="border-t border-gray-100">
            <WorkItemCollab
              apiService={apiService}
              item={item}
              items={items}
              members={members}
              can={{ collaborate: can(role, 'collaborate'), edit: can(role, 'edit') }}
              reasons={{ collaborate: denyReason(project, 'collaborate'), edit: denyReason(project, 'edit') }}
              onChanged={onChanged}
            />
          </div>
        )}
        </div>

        <div className="flex items-center justify-end gap-2 px-6 py-3 border-t border-gray-200 bg-gray-50 rounded-b-xl">
          {effortDirty && <p role="status" className="mr-auto text-xs font-medium text-amber-700">You have unsaved effort changes - use &ldquo;Save effort&rdquo; first. Saving the item does not save them.</p>}
          <Button type="button" variant="outline" onClick={onClose} className="bg-white">
            {canEdit ? 'Cancel' : 'Close'}
          </Button>
          {canEdit && (
            <Button type="submit" form="work-item-form" disabled={saving}>
              {saving ? 'Saving…' : 'Save'}
            </Button>
          )}
        </div>
      </div>
    </div>
  );
};

export default WorkItemModal;
