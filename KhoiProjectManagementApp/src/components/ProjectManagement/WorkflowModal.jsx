import React, { useState } from 'react';
import { X } from 'lucide-react';
import { Button } from '../ui';
import useModalA11y from '../Common/useModalA11y';
import { statusLabel } from './pmConstants';

// Editor for a project's workflow rules: a from -> to grid where each ticked cell is a permitted move.
// The statuses are fixed; only which moves between them are allowed is configurable.
const WorkflowModal = ({ workflow, onSave, onClose }) => {
  const ref = useModalA11y(onClose);
  const [allowed, setAllowed] = useState(() => {
    const map = {};
    workflow.transitions.forEach((t) => { map[`${t.from}>${t.to}`] = t.allowed; });
    return map;
  });
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState(null);

  const toggle = (from, to) => setAllowed((a) => ({ ...a, [`${from}>${to}`]: !a[`${from}>${to}`] }));
  const reset = () => setAllowed((a) => Object.fromEntries(Object.keys(a).map((k) => [k, true])));

  const submit = async (e) => {
    e.preventDefault();
    setSaving(true);
    setError(null);
    try {
      await onSave(workflow.transitions.map((t) => ({ from: t.from, to: t.to, allowed: allowed[`${t.from}>${t.to}`] })));
    } catch (err) {
      setError(err?.message || 'Could not save the workflow.');
      setSaving(false);
    }
  };

  return (
    <div className="fixed inset-0 bg-black/40 z-50 flex items-center justify-center p-4">
      <div ref={ref} role="dialog" aria-modal="true" aria-labelledby="workflow-title" tabIndex={-1}
        className="bg-white rounded-xl shadow-xl w-full max-w-xl max-h-[90vh] overflow-y-auto">
        <div className="flex items-center justify-between px-5 py-4 border-b border-gray-200">
          <h2 id="workflow-title" className="text-lg font-semibold text-gray-900">Workflow rules</h2>
          <button onClick={onClose} aria-label="Close" className="text-gray-400 hover:text-gray-600"><X className="h-5 w-5" /></button>
        </div>
        <form onSubmit={submit} className="p-5 space-y-4">
          {error && <div role="alert" className="text-sm text-red-700 bg-red-50 border border-red-200 rounded-lg px-3 py-2">{error}</div>}
          <p className="text-sm text-gray-600">Tick the moves this project allows. For example, untick To do → Done to force work through In progress first. Everything must still be able to reach Done.</p>
          <table className="w-full text-sm">
            <caption className="sr-only">Allowed status moves</caption>
            <thead>
              <tr>
                <th scope="col" className="text-left text-xs font-medium text-gray-500 py-2">From ↓ / To →</th>
                {workflow.statuses.map((s) => <th key={s} scope="col" className="text-center text-xs font-medium text-gray-500 py-2">{statusLabel(s)}</th>)}
              </tr>
            </thead>
            <tbody>
              {workflow.statuses.map((from) => (
                <tr key={from} className="border-t border-gray-100">
                  <th scope="row" className="text-left font-medium text-gray-800 py-2">{statusLabel(from)}</th>
                  {workflow.statuses.map((to) => (
                    <td key={to} className="text-center py-2">
                      {from === to ? <span className="text-gray-300" aria-hidden="true">–</span> : (
                        <input type="checkbox" checked={!!allowed[`${from}>${to}`]} onChange={() => toggle(from, to)}
                          aria-label={`Allow ${statusLabel(from)} to ${statusLabel(to)}`} className="h-4 w-4 rounded-sm border-gray-300 text-blue-600" />
                      )}
                    </td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
          <div className="flex items-center gap-2 pt-2">
            <button type="button" onClick={reset} className="text-sm text-gray-600 underline">Allow all moves</button>
            <div className="ml-auto flex gap-2">
              <Button type="button" variant="outline" onClick={onClose}>Cancel</Button>
              <Button type="submit" disabled={saving}>{saving ? 'Saving…' : 'Save'}</Button>
            </div>
          </div>
        </form>
      </div>
    </div>
  );
};

export default WorkflowModal;
