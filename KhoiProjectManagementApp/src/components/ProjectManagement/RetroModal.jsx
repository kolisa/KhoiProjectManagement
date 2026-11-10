import React, { useState, useEffect, useCallback } from 'react';
import { X, Trash2, Pencil } from 'lucide-react';
import useModalA11y from '../Common/useModalA11y';
import { useToast } from '../../contexts/ToastContext';
import { useConfirm } from '../../contexts/ConfirmContext';
import { reportApiError } from '../../utils/apiError';

const COLUMNS = [
  { key: 'WentWell', title: 'Went well', tone: 'bg-green-50 border-green-200', placeholder: 'Something that worked…' },
  { key: 'ToImprove', title: 'To improve', tone: 'bg-amber-50 border-amber-200', placeholder: 'Something that got in the way…' },
  { key: 'Action', title: 'Actions', tone: 'bg-blue-50 border-blue-200', placeholder: 'A concrete follow-up…' },
];

const fieldClass = 'w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500';

const RetroColumn = ({ column, items, canAdd, addReason, onAdd, onEdit, onDelete, onToggle }) => {
  const [draft, setDraft] = useState('');
  const [editingId, setEditingId] = useState(null);
  const [editText, setEditText] = useState('');

  const submit = async (e) => {
    e.preventDefault();
    if (!draft.trim()) return;
    if (await onAdd(column.key, draft.trim())) setDraft('');
  };

  const saveEdit = async (item) => {
    if (editText.trim() && (await onEdit(item, editText.trim()))) setEditingId(null);
  };

  return (
    <section aria-label={column.title} className={`rounded-xl border p-3 ${column.tone}`}>
      <h3 className="text-sm font-semibold text-gray-900 mb-2">{column.title} <span className="text-xs font-normal text-gray-500">({items.length})</span></h3>
      <ul className="space-y-2 mb-3">
        {items.map((i) => (
          <li key={i.id} className="bg-white rounded-lg border border-gray-200 px-3 py-2">
            {editingId === i.id ? (
              <div className="space-y-2">
                <textarea aria-label="Edit card" rows={2} maxLength={500} className={fieldClass} value={editText} onChange={(e) => setEditText(e.target.value)} />
                <div className="flex gap-2">
                  <button type="button" onClick={() => saveEdit(i)} className="px-2.5 py-1 text-xs rounded-lg bg-blue-600 text-white">Save</button>
                  <button type="button" onClick={() => setEditingId(null)} className="px-2.5 py-1 text-xs rounded-lg border border-gray-300 text-gray-700">Cancel</button>
                </div>
              </div>
            ) : (
              <>
                <div className="flex items-start gap-2">
                  {column.key === 'Action' && (
                    <input type="checkbox" aria-label={`Mark done: ${i.text}`} checked={i.isDone} onChange={(e) => onToggle(i, e.target.checked)} className="mt-1 h-4 w-4 rounded border-gray-300 text-blue-600" />
                  )}
                  <p className={`flex-1 text-sm whitespace-pre-wrap break-words ${i.isDone ? 'line-through text-gray-400' : 'text-gray-800'}`}>{i.text}</p>
                </div>
                <div className="flex items-center justify-between mt-1 text-xs text-gray-500">
                  <span>{i.authorName}</span>
                  <span className="flex gap-2">
                    {i.canEdit && <button aria-label="Edit card" onClick={() => { setEditingId(i.id); setEditText(i.text); }} className="text-gray-400 hover:text-blue-600"><Pencil className="h-3.5 w-3.5" /></button>}
                    {i.canDelete && <button aria-label="Delete card" onClick={() => onDelete(i)} className="text-gray-400 hover:text-red-600"><Trash2 className="h-3.5 w-3.5" /></button>}
                  </span>
                </div>
              </>
            )}
          </li>
        ))}
        {items.length === 0 && <li className="text-xs text-gray-400 text-center py-2">Nothing yet</li>}
      </ul>
      <form onSubmit={submit} className="flex gap-2" title={!canAdd ? addReason || undefined : undefined}>
        <label className="sr-only" htmlFor={`retro-${column.key}`}>Add to {column.title}{!canAdd && addReason ? ` (${addReason})` : ''}</label>
        <input id={`retro-${column.key}`} className={`${fieldClass} disabled:bg-gray-50 disabled:cursor-not-allowed`} value={draft} maxLength={500} placeholder={column.placeholder}
          disabled={!canAdd} onChange={(e) => setDraft(e.target.value)} />
        <button type="submit" disabled={!canAdd || !draft.trim()} className="px-3 py-2 text-sm rounded-lg bg-blue-600 text-white disabled:opacity-50 disabled:cursor-not-allowed">Add</button>
      </form>
    </section>
  );
};

// Retrospective board for one sprint: went well / to improve / actions. The server decides who can
// edit or delete each card (the flags on each item); `canAdd` just hides the input for read-only roles.
const RetroModal = ({ apiService, sprint, canAdd, addReason, onClose }) => {
  const ref = useModalA11y(onClose);
  const toast = useToast();
  const confirm = useConfirm();
  const [items, setItems] = useState(null);

  const load = useCallback(async () => {
    try {
      setItems(await apiService.getRetro(sprint.id));
    } catch (err) {
      reportApiError(toast, err, 'Could not load the retrospective.');
      setItems([]);
    }
  }, [apiService, sprint.id, toast]);

  useEffect(() => { load(); }, [load]);

  // Each handler returns true on success so a column can clear its input/edit state only then.
  const attempt = async (fn, fallback) => {
    try { await fn(); await load(); return true; } catch (err) { reportApiError(toast, err, fallback); return false; }
  };

  const add = (category, text) => attempt(() => apiService.addRetroItem(sprint.id, category, text), 'Could not add the card.');
  const edit = (item, text) => attempt(() => apiService.updateRetroItem(item.id, item.category, text), 'Could not save the card.');
  const toggle = (item, done) => attempt(() => apiService.setRetroItemDone(item.id, done), 'Could not update the action.');
  const remove = async (item) => {
    if (!(await confirm('Delete this card?', { title: 'Delete card', confirmText: 'Delete', danger: true }))) return;
    await attempt(() => apiService.deleteRetroItem(item.id), 'Could not delete the card.');
  };

  return (
    <div className="fixed inset-0 bg-black/40 z-50 flex items-center justify-center p-4">
      <div ref={ref} role="dialog" aria-modal="true" aria-labelledby="retro-title" tabIndex={-1}
        className="bg-white rounded-xl shadow-xl w-full max-w-5xl max-h-[90vh] overflow-y-auto">
        <div className="flex items-center justify-between px-5 py-4 border-b border-gray-200">
          <h2 id="retro-title" className="text-lg font-semibold text-gray-900">Retrospective · {sprint.name}</h2>
          <button onClick={onClose} aria-label="Close" className="text-gray-400 hover:text-gray-600"><X className="h-5 w-5" /></button>
        </div>
        <div className="p-5">
          {items === null ? <p className="text-sm text-gray-500">Loading…</p> : (
            <div className="grid gap-4 md:grid-cols-3">
              {COLUMNS.map((c) => (
                <RetroColumn key={c.key} column={c} items={items.filter((i) => i.category === c.key)}
                  canAdd={canAdd} addReason={addReason} onAdd={add} onEdit={edit} onDelete={remove} onToggle={toggle} />
              ))}
            </div>
          )}
        </div>
      </div>
    </div>
  );
};

export default RetroModal;
