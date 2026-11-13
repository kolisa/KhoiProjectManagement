import React, { useState } from 'react';
import { X, ArrowUp, ArrowDown } from 'lucide-react';
import { Button, Input, Modal } from '../ui';
import { statusLabel } from './pmConstants';

// Rename, reorder and set WIP limits on the board's columns. Each column is bound to a status (the
// real work-item status), so columns can't be added or removed here - that arrives with configurable
// workflows.
const BoardSettingsModal = ({ columns, onSave, onClose }) => {
  const [rows, setRows] = useState(() =>
    [...columns].sort((a, b) => a.position - b.position).map((c) => ({
      status: c.status, name: c.name, wipLimit: c.wipLimit ?? '',
    })));
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState(null);

  const update = (index, patch) => setRows((r) => r.map((row, i) => (i === index ? { ...row, ...patch } : row)));
  const swap = (a, b) => setRows((r) => {
    const next = [...r];
    [next[a], next[b]] = [next[b], next[a]];
    return next;
  });

  const submit = async (e) => {
    e.preventDefault();
    if (rows.some((r) => !r.name.trim())) { setError('Every column needs a name.'); return; }
    if (rows.some((r) => r.wipLimit !== '' && (!Number.isInteger(Number(r.wipLimit)) || Number(r.wipLimit) < 1 || Number(r.wipLimit) > 100))) {
      setError('WIP limits must be whole numbers between 1 and 100, or empty for no limit.');
      return;
    }
    setSaving(true);
    setError(null);
    try {
      await onSave(rows.map((r) => ({ status: r.status, name: r.name.trim(), wipLimit: r.wipLimit === '' ? null : Number(r.wipLimit) })));
    } catch (err) {
      setError(err?.message || 'Could not save the board.');
      setSaving(false);
    }
  };

  return (
    <Modal onClose={onClose} size="lg" closeOnBackdrop={false} padded={false} titleId="board-settings-title"
      header={(
        <div className="flex items-center justify-between px-5 py-4 border-b border-gray-200">
          <h2 id="board-settings-title" className="text-lg font-semibold text-gray-900">Columns &amp; WIP limits</h2>
          <button onClick={onClose} aria-label="Close" className="text-gray-400 hover:text-gray-600"><X className="h-5 w-5" /></button>
        </div>
      )}>
        <form onSubmit={submit} className="p-5 space-y-3">
          {error && <div role="alert" className="text-sm text-red-700 bg-red-50 border border-red-200 rounded-lg px-3 py-2">{error}</div>}
          <p className="text-xs text-gray-500">A WIP limit stops cards being moved into a column once it is full. Leave empty for no limit.</p>
          {rows.map((row, i) => (
            <div key={row.status} className="flex items-center gap-2">
              <div className="flex flex-col text-gray-400">
                <button type="button" aria-label={`Move ${row.name} column left`} disabled={i === 0} onClick={() => swap(i, i - 1)} className="hover:text-blue-600 disabled:opacity-30"><ArrowUp className="h-3.5 w-3.5" /></button>
                <button type="button" aria-label={`Move ${row.name} column right`} disabled={i === rows.length - 1} onClick={() => swap(i, i + 1)} className="hover:text-blue-600 disabled:opacity-30"><ArrowDown className="h-3.5 w-3.5" /></button>
              </div>
              <Input aria-label={`Name for the ${statusLabel(row.status)} column`} value={row.name} maxLength={40}
                onChange={(e) => update(i, { name: e.target.value })} className="flex-1" />
              <Input aria-label={`WIP limit for ${row.name}`} type="number" min="1" max="100" placeholder="No limit" value={row.wipLimit}
                onChange={(e) => update(i, { wipLimit: e.target.value })} className="w-24" />
            </div>
          ))}
          <div className="flex justify-end gap-2 pt-2">
            <Button type="button" variant="outline" onClick={onClose}>Cancel</Button>
            <Button type="submit" disabled={saving}>{saving ? 'Saving…' : 'Save'}</Button>
          </div>
        </form>
    </Modal>
  );
};

export default BoardSettingsModal;
