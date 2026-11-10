import React, { useState, useMemo } from 'react';
import { Plus, Sparkles, ListChecks } from 'lucide-react';
import { useToast } from '../../contexts/ToastContext';
import { reportApiError } from '../../utils/apiError';
import { statusLabel } from './pmConstants';
import { suggestSubtasks } from './richText';
import Combobox, { peopleOptions } from './Combobox';

const fieldClass = 'border border-gray-300 rounded-lg px-2.5 py-1.5 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-blue-500 disabled:bg-gray-50 disabled:cursor-not-allowed';

// The subtasks of a work item: a quick list with progress, an inline "add", and suggestions drawn from the description.
// Subtasks are ordinary work items (type Subtask, parent = this item) so the board, backlog and sprint effort all see them.
const SubtasksPanel = ({ apiService, project, item, items, members, description, canCreate, reason, onChanged }) => {
  const toast = useToast();
  const [title, setTitle] = useState('');
  const [assignee, setAssignee] = useState(item?.assignedToId ?? '');
  const [busy, setBusy] = useState(false);
  const [suggestions, setSuggestions] = useState(null);   // null = not asked, [] = nothing found
  const [picked, setPicked] = useState(new Set());

  const children = useMemo(() => (item ? items.filter((i) => i.parentId === item.id) : []), [items, item]);
  const done = children.filter((c) => c.status === 'completed').length;
  const memberName = (id) => members.find((m) => m.userId === id)?.name;
  const cannotHaveChildren = item?.type === 'Subtask';
  const blocked = !canCreate || cannotHaveChildren;
  const blockedReason = cannotHaveChildren ? 'A subtask cannot have subtasks of its own.' : reason;

  const create = async (text, assignedToId) => apiService.createWorkItem(project.id, {
    title: text, description: '', type: 'Subtask', status: 'todo', priority: item.priority || 'medium',
    storyPoints: null, assignedToId: assignedToId === '' || assignedToId == null ? null : Number(assignedToId), parentId: item.id, dueDate: null,
  });

  const add = async () => {
    const text = title.trim();
    if (!text || blocked) return;
    setBusy(true);
    try {
      await create(text, assignee);
      setTitle('');
      onChanged?.();
    } catch (err) {
      reportApiError(toast, err, 'Could not add the subtask.');
    } finally { setBusy(false); }
  };

  const toggle = async (child) => {
    try {
      await apiService.moveWorkItem(child.id, child.status === 'completed' ? 'todo' : 'completed', null);
      onChanged?.();
    } catch (err) { reportApiError(toast, err, 'Could not update the subtask.'); }
  };

  const suggest = () => {
    const found = suggestSubtasks(description, children.map((c) => c.title));
    setSuggestions(found);
    setPicked(new Set(found));
  };

  const addPicked = async () => {
    setBusy(true);
    let added = 0;
    try {
      for (const t of suggestions.filter((s) => picked.has(s))) {
        await create(t, assignee);   // one at a time so ranking stays in the order suggested
        added += 1;
      }
      toast.success?.(`Added ${added} subtask${added === 1 ? '' : 's'}.`);
      setSuggestions(null);
    } catch (err) {
      reportApiError(toast, err, `Added ${added}, then something went wrong.`);
    } finally { setBusy(false); onChanged?.(); }
  };

  if (!item) {
    return (
      <section aria-label="Subtasks" className="rounded-xl border border-dashed border-gray-300 px-4 py-3 text-sm text-gray-500">
        Save the item first, then break it into subtasks here - or let the description suggest them.
      </section>
    );
  }

  return (
    <section aria-label="Subtasks" className="rounded-xl border border-gray-200">
      <div className="flex flex-wrap items-center gap-2 border-b border-gray-100 px-4 py-2.5">
        <h3 className="flex items-center gap-1.5 text-sm font-semibold text-gray-900"><ListChecks className="h-4 w-4 text-blue-600" aria-hidden="true" /> Subtasks
          {children.length > 0 && <span className="font-normal text-gray-500">{done}/{children.length} done</span>}</h3>
        <button type="button" onClick={suggest} disabled={blocked} title={blocked ? blockedReason : 'Suggest subtasks from the description'}
          className="ml-auto inline-flex items-center gap-1 rounded-lg border border-gray-300 px-2.5 py-1 text-xs font-medium text-gray-700 hover:bg-gray-50 disabled:opacity-50 disabled:cursor-not-allowed">
          <Sparkles className="h-3.5 w-3.5 text-amber-500" aria-hidden="true" /> Suggest from description
        </button>
      </div>

      {children.length > 0 && (
        <>
          <div className="h-1 bg-gray-100" role="progressbar" aria-label="Subtask progress" aria-valuemin={0} aria-valuemax={children.length} aria-valuenow={done}>
            <div className="h-1 bg-green-500" style={{ width: `${(done / children.length) * 100}%` }} />
          </div>
          <ul className="divide-y divide-gray-100">
            {children.map((c) => (
              <li key={c.id} className="flex items-center gap-3 px-4 py-2">
                <input type="checkbox" aria-label={`Mark ${c.title} ${c.status === 'completed' ? 'not done' : 'done'}`} checked={c.status === 'completed'}
                  disabled={blocked} title={blocked ? blockedReason : undefined} onChange={() => toggle(c)} className="h-4 w-4 rounded border-gray-300 disabled:cursor-not-allowed" />
                <span className={`flex-1 min-w-0 truncate text-sm ${c.status === 'completed' ? 'text-gray-400 line-through' : 'text-gray-900'}`}>{c.title}</span>
                {memberName(c.assignedToId) && <span className="text-xs text-gray-500">{memberName(c.assignedToId)}</span>}
                <span className="text-xs text-gray-500">{statusLabel(c.status)}</span>
              </li>
            ))}
          </ul>
        </>
      )}

      {suggestions && (
        <div role="region" aria-label="Suggested subtasks" className="border-t border-amber-200 bg-amber-50 px-4 py-3">
          {suggestions.length === 0 ? (
            <p className="text-sm text-gray-600">Nothing to suggest yet. Write the steps as a list (or as sentences like "We need to add…") in the description and try again.</p>
          ) : (
            <>
              <p className="mb-2 text-xs font-medium text-gray-700">Suggested from the description - untick any you do not want:</p>
              <ul className="space-y-1">
                {suggestions.map((s) => (
                  <li key={s}>
                    <label className="flex items-center gap-2 text-sm text-gray-900">
                      <input type="checkbox" checked={picked.has(s)} onChange={() => setPicked((p) => { const n = new Set(p); if (n.has(s)) n.delete(s); else n.add(s); return n; })}
                        className="h-4 w-4 rounded border-gray-300" /> {s}
                    </label>
                  </li>
                ))}
              </ul>
            </>
          )}
          <div className="mt-2 flex gap-2">
            {suggestions.length > 0 && (
              <button type="button" onClick={addPicked} disabled={busy || picked.size === 0}
                className="rounded-lg bg-blue-600 px-3 py-1.5 text-xs font-medium text-white hover:bg-blue-700 disabled:opacity-50">
                Add {picked.size} subtask{picked.size === 1 ? '' : 's'}
              </button>
            )}
            <button type="button" onClick={() => setSuggestions(null)} className="rounded-lg border border-gray-300 px-3 py-1.5 text-xs text-gray-700 hover:bg-white">Dismiss</button>
          </div>
        </div>
      )}

      <div className="flex flex-wrap items-center gap-2 border-t border-gray-100 px-4 py-3" title={blocked ? blockedReason : undefined}>
        <label htmlFor="subtask-title" className="sr-only">New subtask</label>
        <input id="subtask-title" value={title} maxLength={200} disabled={blocked} placeholder="Add a subtask…" className={`${fieldClass} flex-1 min-w-[10rem]`}
          onChange={(e) => setTitle(e.target.value)}
          onKeyDown={(e) => { if (e.key === 'Enter') { e.preventDefault(); add(); } }} />
        <label htmlFor="subtask-assignee" className="sr-only">Subtask assignee</label>
        <Combobox id="subtask-assignee" value={assignee} disabled={blocked} onChange={setAssignee} className="w-44"
          options={peopleOptions(members)} emptyLabel="Unassigned" searchPlaceholder="Search people…" />
        <button type="button" onClick={add} disabled={blocked || busy || !title.trim()}
          className="inline-flex items-center gap-1 rounded-lg bg-blue-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed">
          <Plus className="h-4 w-4" aria-hidden="true" /> Add
        </button>
      </div>
      {blocked && blockedReason && <p className="px-4 pb-3 text-xs text-gray-500">{blockedReason}</p>}
    </section>
  );
};

export default SubtasksPanel;
