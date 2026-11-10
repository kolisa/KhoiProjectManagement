import React, { useEffect, useState, useCallback } from 'react';
import { Plus, Trash2, Gauge } from 'lucide-react';
import LoadingSpinner from '../Common/LoadingSpinner';
import { useToast } from '../../contexts/ToastContext';
import { reportApiError } from '../../utils/apiError';
import { EFFORT_CATEGORIES, fmtPoints } from './pmConstants';
import Combobox, { peopleOptions } from './Combobox';

const inputClass = 'w-full border border-gray-300 rounded-lg px-2 py-1 text-sm text-right focus:outline-none focus:ring-2 focus:ring-blue-500 disabled:bg-gray-50 disabled:text-gray-400 disabled:cursor-not-allowed';

// A cell in the editor: text (what is typed) or N/A. Empty text = not allocated (nothing is saved for it).
const cellFrom = (c) => (c ? (c.points == null ? { text: '', na: true } : { text: String(c.points), na: false }) : { text: '', na: false });

const rowFromServer = (p) => ({
  userId: p.userId,
  cells: Object.fromEntries(EFFORT_CATEGORIES.map((cat) => [cat.key, cellFrom(p.cells.find((c) => c.category === cat.key))])),
});

const emptyRow = (userId) => ({ userId, cells: Object.fromEntries(EFFORT_CATEGORIES.map((c) => [c.key, { text: '', na: false }])) });

const rowTotal = (row) => EFFORT_CATEGORIES.reduce((sum, c) => {
  const cell = row.cells[c.key];
  const n = cell.na ? 0 : Number(cell.text);
  return sum + (Number.isFinite(n) ? n : 0);
}, 0);

// Per-person effort for one work item: one row per person, one column per category (Analysis & design, Dev effort, Dev
// testing, Deployment, Demo, Monitoring, Training). N/A is distinct from 0. A person's sprint points are these numbers summed.
const EffortEditor = ({ apiService, item, members, canEdit, reason, onSaved }) => {
  const toast = useToast();
  const [rows, setRows] = useState(null);
  const [saving, setSaving] = useState(false);
  const [dirty, setDirty] = useState(false);

  const load = useCallback(async () => {
    try {
      const data = await apiService.getWorkItemEffort(item.id);
      setRows(data.people.map(rowFromServer));
      setDirty(false);
    } catch (err) {
      setRows([]);
      reportApiError(toast, err, 'Could not load the effort.');
    }
  }, [apiService, item.id, toast]);

  useEffect(() => { load(); }, [load]);

  const used = new Set((rows || []).map((r) => r.userId));
  const available = members.filter((m) => !used.has(m.userId));
  const nameOf = (id) => members.find((m) => m.userId === id)?.name || `User ${id}`;

  const update = (userId, fn) => { setRows((rs) => rs.map((r) => (r.userId === userId ? fn(r) : r))); setDirty(true); };
  const setCell = (userId, key, patch) => update(userId, (r) => ({ ...r, cells: { ...r.cells, [key]: { ...r.cells[key], ...patch } } }));
  const addPerson = (userId) => { setRows((rs) => [...rs, emptyRow(userId)]); setDirty(true); };

  const invalid = (rows || []).some((r) => EFFORT_CATEGORIES.some((c) => {
    const cell = r.cells[c.key];
    if (cell.na || cell.text === '') return false;
    const n = Number(cell.text);
    return !Number.isFinite(n) || n < 0 || n > 1000;
  }));

  const save = async () => {
    const people = rows.map((r) => ({
      userId: r.userId,
      cells: EFFORT_CATEGORIES.filter((c) => r.cells[c.key].na || r.cells[c.key].text !== '')
        .map((c) => ({ category: c.key, points: r.cells[c.key].na ? null : Number(r.cells[c.key].text) })),
    })).filter((p) => p.cells.length > 0);
    setSaving(true);
    try {
      const data = await apiService.saveWorkItemEffort(item.id, people);
      setRows(data.people.map(rowFromServer));
      setDirty(false);
      toast.success?.('Effort saved.');
      onSaved?.();
    } catch (err) {
      reportApiError(toast, err, 'Could not save the effort.');
    } finally { setSaving(false); }
  };

  if (rows === null) return <LoadingSpinner text="Loading effort..." />;
  const grand = rows.reduce((s, r) => s + rowTotal(r), 0);

  return (
    <section aria-label="Effort" className="rounded-xl border border-gray-200" onKeyDown={(e) => { if (e.key === 'Enter' && e.target.tagName === 'INPUT') e.preventDefault(); }}>
      <div className="flex flex-wrap items-center gap-2 border-b border-gray-100 px-4 py-2.5">
        <h3 className="flex items-center gap-1.5 text-sm font-semibold text-gray-900"><Gauge className="h-4 w-4 text-blue-600" aria-hidden="true" /> Effort by person
          <span className="font-normal text-gray-500">{fmtPoints(grand)} points</span></h3>
        <p className="ml-auto text-xs text-gray-500">Counts toward each person&apos;s points in the sprint. Allocate on the subtasks <em>or</em> on this item, not both.</p>
      </div>

      {rows.length === 0 && <p className="px-4 py-3 text-sm text-gray-500">No effort allocated yet.</p>}

      <div className="divide-y divide-gray-100">
        {rows.map((r) => (
          <div key={r.userId} role="group" aria-label={`Effort for ${nameOf(r.userId)}`} className="px-4 py-3">
            <div className="mb-2 flex items-center gap-2">
              <span className="text-sm font-medium text-gray-900">{nameOf(r.userId)}</span>
              <span className="text-xs text-gray-500">{fmtPoints(rowTotal(r))} points</span>
              <button type="button" aria-label={`Remove ${nameOf(r.userId)} from the effort`} disabled={!canEdit} title={reason || undefined}
                onClick={() => { setRows((rs) => rs.filter((x) => x.userId !== r.userId)); setDirty(true); }}
                className="ml-auto text-gray-400 hover:text-red-600 disabled:text-gray-300 disabled:hover:text-gray-300 disabled:cursor-not-allowed"><Trash2 className="h-4 w-4" /></button>
            </div>
            <div className="grid grid-cols-2 gap-x-3 gap-y-2 sm:grid-cols-4 lg:grid-cols-7">
              {EFFORT_CATEGORIES.map((c) => {
                const cell = r.cells[c.key];
                const id = `effort-${r.userId}-${c.key}`;
                return (
                  <div key={c.key}>
                    <label htmlFor={id} className="mb-0.5 block text-[11px] font-medium text-gray-600">{c.label}</label>
                    <input id={id} type="number" inputMode="decimal" min="0" max="1000" step="0.5" value={cell.na ? '' : cell.text}
                      placeholder={cell.na ? 'N/A' : '0'} disabled={!canEdit || cell.na} title={!canEdit ? reason || undefined : undefined}
                      onChange={(e) => setCell(r.userId, c.key, { text: e.target.value })} className={inputClass} />
                    <label className="mt-0.5 flex items-center gap-1 text-[11px] text-gray-500">
                      <input type="checkbox" checked={cell.na} disabled={!canEdit} aria-label={`${c.label} is not applicable for ${nameOf(r.userId)}`}
                        onChange={(e) => setCell(r.userId, c.key, { na: e.target.checked, text: e.target.checked ? '' : cell.text })} className="h-3 w-3 rounded border-gray-300" /> N/A
                    </label>
                  </div>
                );
              })}
            </div>
          </div>
        ))}
      </div>

      <div className="flex flex-wrap items-center gap-2 border-t border-gray-100 px-4 py-3" title={!canEdit ? reason || undefined : undefined}>
        <label htmlFor="effort-add-person" className="sr-only">Add a person to the effort</label>
        <Combobox id="effort-add-person" value="" disabled={!canEdit || available.length === 0} className="w-60" size="sm"
          onChange={(v) => { if (v !== '') addPerson(Number(v)); }} options={peopleOptions(available)} searchPlaceholder="Search people…"
          placeholder={available.length === 0 ? 'Everyone on the project is listed' : 'Add a person…'} />
        <Plus className="h-4 w-4 text-gray-400" aria-hidden="true" />
        <button type="button" onClick={save} disabled={!canEdit || saving || !dirty || invalid}
          className="ml-auto rounded-lg bg-blue-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed">
          {saving ? 'Saving…' : 'Save effort'}
        </button>
      </div>
      {invalid && <p role="alert" className="px-4 pb-3 text-xs text-red-600">Points must be a number between 0 and 1000.</p>}
      {!canEdit && reason && <p className="px-4 pb-3 text-xs text-gray-500">{reason}</p>}
    </section>
  );
};

export default EffortEditor;
