import React, { useState, useEffect, useCallback } from 'react';
import LoadingSpinner from '../Common/LoadingSpinner';
import ErrorMessage from '../Common/ErrorMessage';
import { useToast } from '../../contexts/ToastContext';
import { reportApiError } from '../../utils/apiError';

// SuperAdmin-only: who may enter the Project Management module, and who is confined to it.
// "Module only" accounts see nothing but the PM workspace and the API rejects every other module.
const PmAccessAdmin = ({ apiService }) => {
  const toast = useToast();
  const [users, setUsers] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [savingId, setSavingId] = useState(null);

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
    const next = { pmAccess: user.pmAccess, pmOnly: user.pmOnly, ...patch };
    if (!next.pmAccess) next.pmOnly = false; // revoking access also lifts the confinement
    setSavingId(user.id);
    try {
      await apiService.setPmAccess(user.id, next.pmAccess, next.pmOnly);
      setUsers((list) => list.map((u) => (u.id === user.id ? { ...u, ...next } : u)));
      toast.success(`Updated ${user.name}. Takes effect at their next sign-in or token refresh.`);
    } catch (err) {
      reportApiError(toast, err);
    } finally {
      setSavingId(null);
    }
  };

  if (loading) return <LoadingSpinner />;
  if (error) return <ErrorMessage message={error} onRetry={load} />;

  return (
    <div className="bg-white border border-gray-200 rounded-xl overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="text-left text-xs uppercase tracking-wide text-gray-500 border-b border-gray-200">
            <th className="px-4 py-3 font-semibold">User</th>
            <th className="px-4 py-3 font-semibold">Module access</th>
            <th className="px-4 py-3 font-semibold">Module only</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-gray-100">
          {users.map((u) => (
            <tr key={u.id}>
              <td className="px-4 py-3">
                <p className="font-medium text-gray-900">{u.name}</p>
                <p className="text-xs text-gray-500">{u.email}</p>
              </td>
              <td className="px-4 py-3">
                <input type="checkbox" aria-label={`Project Management access for ${u.name}`} checked={u.pmAccess} disabled={savingId === u.id}
                  onChange={(e) => update(u, { pmAccess: e.target.checked })} className="h-4 w-4 rounded border-gray-300 text-blue-600" />
              </td>
              <td className="px-4 py-3">
                <input type="checkbox" aria-label={`Confine ${u.name} to the Project Management module`} checked={u.pmOnly} disabled={!u.pmAccess || savingId === u.id}
                  onChange={(e) => update(u, { pmOnly: e.target.checked })} className="h-4 w-4 rounded border-gray-300 text-blue-600 disabled:opacity-40" />
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
};

export default PmAccessAdmin;
