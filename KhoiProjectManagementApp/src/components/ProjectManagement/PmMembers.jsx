import React, { useState, useEffect, useCallback } from 'react';
import { Trash2, Upload, UserPlus, ChevronRight } from 'lucide-react';
import LoadingSpinner from '../Common/LoadingSpinner';
import ErrorMessage from '../Common/ErrorMessage';
import { useToast } from '../../contexts/ToastContext';
import { useConfirm } from '../../contexts/ConfirmContext';
import { reportApiError } from '../../utils/apiError';
import { PROJECT_ROLES, roleLabel, can, denyReason } from './pmConstants';
import { Avatar, AccessBadge, cardClass } from './pmUi';
import ImportMembersModal from './ImportMembersModal';
import RoleMatrix from './RoleMatrix';

const selectClass = 'w-full border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-blue-500';
const labelClass = 'block text-xs font-medium text-gray-600 mb-1';

// The project's people: who is on it, what role each has and what that means they can do (Read only / Read & write /
// Full control). Only a Project Admin (or SuperAdmin) can change it; everyone else sees it read-only. People who can be
// added are those who already have module access (an administrator can grant it from the import).
const PmMembers = ({ apiService, project, onChanged, otherProjects = [], canGrantAccess = false, currentUserId = null }) => {
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
  const manageReason = denyReason(project, 'members');

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
    // The signed-in person goes LAST: if the change takes their own access away (removing or demoting themselves),
    // the remaining calls would otherwise fail with confusing "not found" errors.
    const people = members.filter((m) => selected.has(m.userId))
      .sort((a, b) => (a.userId === currentUserId) - (b.userId === currentUserId));
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

  const includesMe = currentUserId != null && selected.has(currentUserId);

  const bulkSetRole = async () => {
    // Giving yourself a role that cannot manage members would lock you out of this page's controls.
    if (includesMe && !can(bulkRole, 'members')
      && !(await confirm(`This includes you. As ${roleLabel(bulkRole)} you will no longer be able to manage this project's members.`, { title: 'Change your own role?', confirmText: 'Change roles', danger: true }))) return;
    await bulk((m) => apiService.setPmMember(project.id, m.userId, bulkRole), 'updated');
  };
  const bulkRemove = async () => {
    const people = `${selected.size} ${selected.size === 1 ? 'person' : 'people'}`;
    const message = `Remove ${people} from this project? Their assigned items become unassigned.`
      + (includesMe ? ' This includes you - you will lose access to this project.' : '');
    if (!(await confirm(message, { title: 'Remove members', confirmText: 'Remove', danger: true }))) return;
    await bulk((m) => apiService.removePmMember(project.id, m.userId), 'removed');
  };

  if (loading) return <LoadingSpinner />;
  if (error) return <ErrorMessage message={error} onRetry={load} />;

  const memberIds = new Set(members.map((m) => m.userId));
  const addable = candidates.filter((c) => !memberIds.has(c.id));
  const everyoneSelected = members.length > 0 && members.every((m) => selected.has(m.userId));

  return (
    <div className="space-y-4">
      {(
        <section aria-label="Add people" title={manageReason || undefined} className={`${cardClass} p-4`}>
          <h2 className="mb-3 flex items-center gap-2 text-sm font-semibold text-gray-900">
            <UserPlus className="h-4 w-4 text-blue-600" aria-hidden="true" /> Add people to {project.name}
          </h2>
          <form onSubmit={add} className="flex flex-wrap items-end gap-3">
            <div className="min-w-[14rem] flex-1">
              <label htmlFor="pm-add-person" className={labelClass}>Person</label>
              <select id="pm-add-person" disabled={!canManage} className={`${selectClass} disabled:bg-gray-50 disabled:cursor-not-allowed`} value={newUserId} onChange={(e) => setNewUserId(e.target.value)}>
                <option value="">Choose a person…</option>
                {addable.map((u) => <option key={u.id} value={u.id}>{u.name} ({u.email})</option>)}
              </select>
            </div>
            <div className="w-44">
              <label htmlFor="pm-add-role" className={labelClass}>Role</label>
              <select id="pm-add-role" disabled={!canManage} className={`${selectClass} disabled:bg-gray-50 disabled:cursor-not-allowed`} value={newRole} onChange={(e) => setNewRole(e.target.value)}>
                {PROJECT_ROLES.map((r) => <option key={r.key} value={r.key}>{r.label}</option>)}
              </select>
            </div>
            <button type="submit" disabled={!canManage || !newUserId} className="rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed">Add to project</button>
            <button type="button" onClick={() => setShowImport(true)} disabled={!canManage} className="ml-auto disabled:opacity-50 disabled:cursor-not-allowed inline-flex items-center gap-1.5 rounded-lg border border-gray-300 px-3 py-2 text-sm text-gray-700 hover:bg-gray-50">
              <Upload className="h-4 w-4" aria-hidden="true" /> Import members
            </button>
          </form>
          {!canManage && manageReason && <p className="mt-2 text-xs text-gray-500">{manageReason}</p>}
          {canManage && addable.length === 0 && <p className="mt-2 text-xs text-gray-500">Everyone who has Project Management access is already on this project.</p>}
        </section>
      )}

      <section aria-label="Members" className={cardClass}>
        <div className="flex flex-wrap items-center gap-3 border-b border-gray-100 px-4 py-3">
          <h2 className="text-sm font-semibold text-gray-900">Members <span className="font-normal text-gray-500">({members.length})</span></h2>
          {members.length > 0 && (
            <div className="ml-auto flex flex-wrap items-center gap-2 text-sm">
              <label className="inline-flex items-center gap-2 text-gray-700">
                <input type="checkbox" className="h-4 w-4 rounded border-gray-300" aria-label="Select everyone" disabled={!canManage} title={manageReason || undefined} checked={everyoneSelected}
                  onChange={() => setSelected(everyoneSelected ? new Set() : new Set(members.map((m) => m.userId)))} />
                {selected.size > 0 ? `${selected.size} selected` : 'Select'}
              </label>
              {selected.size > 0 && (
                <>
                  <select aria-label="Role for selected people" className="rounded-lg border border-gray-300 bg-white px-2 py-1.5 text-sm" value={bulkRole} onChange={(e) => setBulkRole(e.target.value)}>
                    {PROJECT_ROLES.map((r) => <option key={r.key} value={r.key}>{r.label}</option>)}
                  </select>
                  <button onClick={bulkSetRole} className="rounded-lg border border-gray-300 px-3 py-1.5 text-gray-700 hover:bg-gray-50">Set role</button>
                  <button onClick={bulkRemove} className="rounded-lg border border-red-300 px-3 py-1.5 text-red-700 hover:bg-red-50">Remove selected</button>
                </>
              )}
            </div>
          )}
        </div>

        <ul className="divide-y divide-gray-100">
          {members.map((m) => (
            <li key={m.userId} className="flex flex-wrap items-center gap-3 px-4 py-3">
              <input type="checkbox" className="h-4 w-4 rounded border-gray-300 disabled:cursor-not-allowed" aria-label={`Select ${m.name}`} disabled={!canManage} title={manageReason || undefined}
                checked={selected.has(m.userId)} onChange={() => toggle(m.userId)} />
              <Avatar name={m.name} />
              <div className="min-w-0 flex-1">
                <p className="truncate text-sm font-medium text-gray-900">
                  {m.name}{m.userId === currentUserId && <span className="ml-1.5 text-xs font-normal text-gray-500">(you)</span>}
                </p>
                <p className="truncate text-xs text-gray-500">{m.email}</p>
              </div>
              <AccessBadge role={m.role} readOnly={m.moduleReadOnly} />
                <select aria-label={`Role for ${m.name}`} disabled={!canManage} title={manageReason || undefined} className="rounded-lg border border-gray-300 bg-white px-2.5 py-1.5 text-sm disabled:bg-gray-50 disabled:text-gray-500 disabled:cursor-not-allowed" value={m.role}
                  onChange={(e) => run(() => apiService.setPmMember(project.id, m.userId, e.target.value), 'Role updated.')}>
                  {PROJECT_ROLES.map((r) => <option key={r.key} value={r.key}>{r.label}</option>)}
                </select>
              <button aria-label={`Remove ${m.name}`} onClick={() => remove(m)} disabled={!canManage} title={manageReason || undefined} className="text-gray-400 hover:text-red-600 disabled:text-gray-300 disabled:hover:text-gray-300 disabled:cursor-not-allowed">
                <Trash2 className="h-4 w-4" />
              </button>
            </li>
          ))}
        </ul>
      </section>

      <details className={`${cardClass} group`}>
        <summary className="flex cursor-pointer list-none items-center gap-2 px-4 py-3 text-sm font-semibold text-gray-900">
          <ChevronRight className="h-4 w-4 text-gray-400 transition-transform group-open:rotate-90" aria-hidden="true" />
          What can each role do?
        </summary>
        <div className="border-t border-gray-100 px-4 py-4"><RoleMatrix highlightRole={project.actualRole} /></div>
      </details>

      {showImport && (
        <ImportMembersModal apiService={apiService} project={project} otherProjects={otherProjects} canGrantAccess={canGrantAccess}
          onImported={() => { load(); onChanged?.(); }} onClose={() => setShowImport(false)} />
      )}
    </div>
  );
};

export default PmMembers;
