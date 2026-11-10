import React, { useState, useEffect, useCallback } from 'react';
import { Trash2 } from 'lucide-react';
import LoadingSpinner from '../Common/LoadingSpinner';
import ErrorMessage from '../Common/ErrorMessage';
import { useToast } from '../../contexts/ToastContext';
import { useConfirm } from '../../contexts/ConfirmContext';
import { reportApiError } from '../../utils/apiError';
import { PROJECT_ROLES, roleLabel, can } from './pmConstants';

const selectClass = 'border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-blue-500';

// Project roster with per-project roles. Only a Project Admin (or SuperAdmin) can change it; everyone
// else sees it read-only. Candidates for "add" are users a SuperAdmin has granted module access.
const PmMembers = ({ apiService, project, onChanged }) => {
  const toast = useToast();
  const confirm = useConfirm();
  const [members, setMembers] = useState([]);
  const [candidates, setCandidates] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [newUserId, setNewUserId] = useState('');
  const [newRole, setNewRole] = useState('Developer');
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
          {addable.length === 0 && <span className="text-xs text-gray-500">Everyone with module access is already on this project.</span>}
        </form>
      )}

      <ul className="bg-white border border-gray-200 rounded-xl divide-y divide-gray-100">
        {members.map((m) => (
          <li key={m.userId} className="flex items-center gap-3 px-4 py-3">
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
    </div>
  );
};

export default PmMembers;
