import React, { useState, useEffect, useCallback, useMemo } from 'react';
import { Search, Eye, Pencil, Lock, AlertTriangle } from 'lucide-react';
import LoadingSpinner from '../Common/LoadingSpinner';
import ErrorMessage from '../Common/ErrorMessage';
import { Avatar, cardClass } from './pmUi';
import { useToast } from '../../contexts/ToastContext';
import { reportApiError } from '../../utils/apiError';

const LEVELS = [
  { key: 'none', label: 'No access' },
  { key: 'read', label: 'Read only' },
  { key: 'write', label: 'Read & write' },
];

export const levelOf = (u) => (!u.pmAccess ? 'none' : u.pmReadOnly ? 'read' : 'write');

// SuperAdmin-only: the per-person policy for the whole module. Each person gets one access LEVEL -
// no access, read only, or read & write - and can optionally be confined to the module. The level caps what they can
// do in every project; their project role then decides the detail within it. Changes apply on their next request.
const PmAccessAdmin = ({ apiService }) => {
  const toast = useToast();
  const [users, setUsers] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [savingId, setSavingId] = useState(null);
  const [query, setQuery] = useState('');

  const load = useCallback(async () => {
    try {
      setError(null);
      setUsers(await apiService.getPmAccessUsers());
    } catch (err) {
      setError(err?.message || 'Could not load users.');
    } finally {
      setLoading(false);
    }
  }, [apiService]);

  useEffect(() => { load(); }, [load]);

  const update = async (user, patch) => {
    const level = patch.level ?? levelOf(user);
    const pmOnly = level === 'none' ? false : (patch.pmOnly ?? user.pmOnly);   // revoking access also lifts the confinement
    setSavingId(user.id);
    try {
      await apiService.setPmAccess(user.id, level !== 'none', pmOnly, level === 'read');
      setUsers((list) => list.map((u) => (u.id === user.id ? { ...u, pmAccess: level !== 'none', pmOnly, pmReadOnly: level === 'read' } : u)));
      toast.success(`Updated ${user.name}. Takes effect on their next request.`);
    } catch (err) {
      reportApiError(toast, err);
    } finally {
      setSavingId(null);
    }
  };

  const shown = useMemo(() => {
    const q = query.trim().toLowerCase();
    return q ? users.filter((u) => u.name.toLowerCase().includes(q) || u.email.toLowerCase().includes(q)) : users;
  }, [users, query]);

  if (loading) return <LoadingSpinner />;
  if (error) return <ErrorMessage message={error} onRetry={load} />;

  return (
    <div className="space-y-4">
      <section aria-label="Access levels explained" className={`${cardClass} grid gap-4 p-4 sm:grid-cols-3`}>
        <div className="flex gap-3">
          <Eye className="mt-0.5 h-5 w-5 flex-shrink-0 text-gray-500" aria-hidden="true" />
          <div><p className="text-sm font-semibold text-gray-900">Read only</p><p className="text-xs text-gray-500">Can see everything on the projects they belong to, and change nothing, whatever their project role.</p></div>
        </div>
        <div className="flex gap-3">
          <Pencil className="mt-0.5 h-5 w-5 flex-shrink-0 text-blue-600" aria-hidden="true" />
          <div><p className="text-sm font-semibold text-gray-900">Read &amp; write</p><p className="text-xs text-gray-500">Can read and change work as their project role allows (a Viewer role stays read only).</p></div>
        </div>
        <div className="flex gap-3">
          <Lock className="mt-0.5 h-5 w-5 flex-shrink-0 text-violet-600" aria-hidden="true" />
          <div><p className="text-sm font-semibold text-gray-900">Module only</p><p className="text-xs text-gray-500">Confines the account to this module: it cannot open any other part of the app.</p></div>
        </div>
      </section>

      <div className="relative max-w-sm">
        <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-gray-400" aria-hidden="true" />
        <input aria-label="Find a person" placeholder="Find a person" value={query} onChange={(e) => setQuery(e.target.value)}
          className="w-full rounded-lg border border-gray-300 py-2 pl-9 pr-3 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
      </div>

      <div className={`${cardClass} overflow-x-auto`}>
        <table className="w-full text-sm">
          <thead>
            <tr className="border-b border-gray-200 text-left text-xs uppercase tracking-wide text-gray-500">
              <th className="px-4 py-3 font-semibold">Person</th>
              <th className="px-4 py-3 font-semibold">Projects</th>
              <th className="px-4 py-3 font-semibold">Access level</th>
              <th className="px-4 py-3 font-semibold">Module only</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {shown.map((u) => {
              const level = levelOf(u);
              return (
                <tr key={u.id}>
                  <td className="px-4 py-3">
                    <div className="flex items-center gap-3">
                      <Avatar name={u.name} />
                      <div className="min-w-0"><p className="truncate font-medium text-gray-900">{u.name}</p><p className="truncate text-xs text-gray-500">{u.email}</p></div>
                    </div>
                  </td>
                  <td className="px-4 py-3 text-gray-700">
                    {u.projectCount ?? 0}
                    {level === 'none' && u.projectCount > 0 && (
                      <span className="ml-2 inline-flex items-center gap-1 rounded-full bg-amber-50 px-2 py-0.5 text-xs font-medium text-amber-700"
                        title="This person is on projects but cannot see them in Project Management until they are given access">
                        <AlertTriangle className="h-3 w-3" aria-hidden="true" /> Can't see them
                      </span>
                    )}
                  </td>
                  <td className="px-4 py-3">
                    <select aria-label={`Access level for ${u.name}`} value={level} disabled={savingId === u.id} onChange={(e) => update(u, { level: e.target.value })}
                      className="rounded-lg border border-gray-300 bg-white px-2.5 py-1.5 text-sm">
                      {LEVELS.map((l) => <option key={l.key} value={l.key}>{l.label}</option>)}
                    </select>
                  </td>
                  <td className="px-4 py-3">
                    <input type="checkbox" aria-label={`Confine ${u.name} to the Project Management module`} checked={u.pmOnly} disabled={level === 'none' || savingId === u.id}
                      onChange={(e) => update(u, { pmOnly: e.target.checked })} className="h-4 w-4 rounded border-gray-300 text-blue-600 disabled:opacity-40" />
                  </td>
                </tr>
              );
            })}
            {shown.length === 0 && <tr><td colSpan={4} className="px-4 py-8 text-center text-gray-500">No one matches.</td></tr>}
          </tbody>
        </table>
      </div>
    </div>
  );
};

export default PmAccessAdmin;
