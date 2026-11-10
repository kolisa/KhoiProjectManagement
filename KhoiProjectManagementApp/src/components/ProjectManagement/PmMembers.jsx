import React, { useState, useEffect, useCallback } from 'react';
import { Trash2, Upload } from 'lucide-react';
import LoadingSpinner from '../Common/LoadingSpinner';
import ErrorMessage from '../Common/ErrorMessage';
import { useToast } from '../../contexts/ToastContext';
import { useConfirm } from '../../contexts/ConfirmContext';
import { reportApiError } from '../../utils/apiError';
import { PROJECT_ROLES, roleLabel, can } from './pmConstants';
import ImportMembersModal from './ImportMembersModal';

const selectClass = 'border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-blue-500';

// Project roster with per-project roles. Only a Project Admin (or SuperAdmin) can change it; everyone
// else sees it read-only. Candidates for "add" are users a SuperAdmin has granted module access.
const PmMembers = ({ apiService, project, onChanged, otherProjects = [], canGrantAccess = false }) => {
  const toast = useToast();
  const confirm = useConfirm();
  const [members, setMembers] = useState([]);
  const [candidates, setCandidates] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [newUserId, setNewUserId] = useState('');
  const [newRole, setNewRole] = useState('Developer');
  const [showImport, setShowImport] = useState(false);
  const [selected, setSelected] = useState(() => new Set());
  const [bulkRole, setBulkRole] = useState('Developer');
  const canManage = can(project.myRole, 'members');

  const load = useCallback(async () => {
    try {
      setError(null);
      const [m, c] = await Promise.all([
        apiService.getPmMembers(project.id),
        canManage ? apiService.getPmUsers() : Promise.resolve([]),
      ]);
      setMembers(m);
      setCandidates(c);
    } catch (err) {
      setError(err?.message || 'Could not load members.');
    } finally {
      setLoading(false);
    }
  }, [apiService, project.id, canManage]);

  useEffect(() => { setLoading(true); load(); }, [load]);

  const run = async (action, success) => {
    try {
      await action();
      toast.success(success);
      await load();
      onChanged?.();
    } catch (err) {
      reportApiError(toast, err);
    }
  };

  const add = (e) => {
    e.preventDefault();
    if (!newUserId) return;
    run(() => apiService.setPmMember(project.id, Number(newUserId), newRole), 'Member added.')
      .then(() => setNewUserId(''));
  };

  const remove = async (member) => {
    if (!(await confirm(`Remove ${member.name} from this project? Their assigned items become unassigned.`, { title: 'Remove member', confirmText: 'Remove', danger: true }))) return;
    run(() => apiService.removePmMember(project.id, member.userId), 'Member removed.');
  };

  const toggle = (id) => setSelected((prev) => {
    const next = new Set(prev);
    if (next.has(id)) next.delete(id); else next.add(id);
    return next;
  });

  // Bulk actions run one person at a time through the same endpoints as the single actions, so every server
  // rule (e.g. keeping at least one Project Admin) still applies and one refusal does not undo the rest.
  const bulk = async (action, verb) => {
    const people = members.filter((m) => selected.has(m.userId));
    const failures = [];
    for (const m of people) {
      try { await action(m); } catch (err) { failures.push(`${m.name}: ${err?.message || 'failed'}`); }
    }
    const done = people.length - failures.length;
    if (done > 0) toast.success(`${done} ${done === 1 ? 'person' : 'people'} ${verb}.`);
    if (failures.length > 0) toast.error(failures.slice(0, 3).join(' · ') + (failures.length > 3 ? ` · and ${failures.length - 3} more` : ''));
    setSelected(new Set());
    await load();
    onChanged?.();
  };

  const bulkSetRole = () => bulk((m) => apiService.setPmMember(project.id, m.userId, bulkRole), 'updated');
  const bulkRemove = async () => {
    if (!(await confirm(`Remove ${selected.size} ${selected.size === 1 ? 'person' : 'people'} from this project? Their assigned items become unassigned.`, { title: 'Remove members', confirmText: 'Remove', danger: true }))) return;
    await bulk((m) => apiService.removePmMember(project.id, m.userId), 'removed');
  };

  if (loading) return <LoadingSpinner />;
  if (error) return <ErrorMessage message={error} onRetry={load} />;

  const memberIds = new Set(members.map((m) => m.userId));
  const addable = candidates.filter((c) => !memberIds.has(c.id));

  return (
    <div className="space-y-4">
      {canManage && (
        <form onSubmit={add} className="flex flex-wrap items-center gap-2">
          <select aria-label="User to add" className={selectClass} value={newUserId} onChange={(e) => setNewUserId(e.target.value)}>
            <option value="">Add a person…</option>
            {addable.map((u) => <option key={u.id} value={u.id}>{u.name} ({u.email})</option>)}
          </select>
          <select aria-label="Role" className={selectClass} value={newRole} onChange={(e) => setNewRole(e.target.value)}>
            {PROJECT_ROLES.map((r) => <option key={r.key} value={r.key}>{r.label}</option>)}
          </select>
          <button type="submit" disabled={!newUserId} className="px-4 py-2 rounded-lg bg-blue-600 text-white text-sm font-medium hover:bg-blue-700 disabled:opacity-50">Add</button>
          <button type="button" onClick={() => setShowImport(true)} className="ml-auto inline-flex items-center gap-1.5 px-3 py-2 rounded-lg border border-gray-300 text-sm text-gray-700 hover:bg-gray-50">
            <Upload className="h-4 w-4" aria-hidden="true" /> Import members
          </button>
          {addable.length === 0 && <span className="text-xs text-gray-500">Everyone with module access is already on this project.</span>}
        </form>
      )}

      {canManage && members.length > 0 && (
        <div className="flex flex-wrap items-center gap-2 text-sm">
          <label className="inline-flex items-center gap-2 text-gray-700">
            <input type="checkbox" className="h-4 w-4 rounded border-gray-300" aria-label="Select everyone"
              checked={members.length > 0 && members.every((m) => selected.has(m.userId))}
              onChange={() => setSelected(members.every((m) => selected.has(m.userId)) ? new Set() : new Set(members.map((m) => m.userId)))} />
            {selected.size > 0 ? `${selected.size} selected` : 'Select'}
          </label>
          {selected.size > 0 && (
            <>
              <select aria-label="Role for selected people" className={selectClass} value={bulkRole} onChange={(e) => setBulkRole(e.target.value)}>
                {PROJECT_ROLES.map((r) => <option key={r.key} value={r.key}>{r.label}</option>)}
              </select>
              <button onClick={bulkSetRole} className="px-3 py-2 rounded-lg border border-gray-300 text-gray-700 hover:bg-gray-50">Set role</button>
              <button onClick={bulkRemove} className="px-3 py-2 rounded-lg border border-red-300 text-red-700 hover:bg-red-50">Remove selected</button>
            </>
          )}
        </div>
      )}

      <ul className="bg-white border border-gray-200 rounded-xl divide-y divide-gray-100">
        {members.map((m) => (
          <li key={m.userId} className="flex items-center gap-3 px-4 py-3">
            {canManage && (
              <input type="checkbox" className="h-4 w-4 rounded border-gray-300" aria-label={`Select ${m.name}`}
                checked={selected.has(m.userId)} onChange={() => toggle(m.userId)} />
            )}
            <div className="flex-1 min-w-0">
              <p className="text-sm font-medium text-gray-900 truncate">{m.name}</p>
              <p className="text-xs text-gray-500 truncate">{m.email}</p>
            </div>
            {canManage ? (
              <select
                aria-label={`Role for ${m.name}`}
                className={selectClass}
                value={m.role}
                onChange={(e) => run(() => apiService.setPmMember(project.id, m.userId, e.target.value), 'Role updated.')}
              >
                {PROJECT_ROLES.map((r) => <option key={r.key} value={r.key}>{r.label}</option>)}
              </select>
            ) : (
              <span className="text-sm text-gray-600">{roleLabel(m.role)}</span>
            )}
            {canManage && (
              <button aria-label={`Remove ${m.name}`} onClick={() => remove(m)} className="text-gray-400 hover:text-red-600">
                <Trash2 className="h-4 w-4" />
              </button>
            )}
          </li>
        ))}
      </ul>

      {showImport && (
        <ImportMembersModal apiService={apiService} project={project} otherProjects={otherProjects} canGrantAccess={canGrantAccess}
          onImported={() => { load(); onChanged?.(); }} onClose={() => setShowImport(false)} />
      )}
    </div>
  );
};

export default PmMembers;
