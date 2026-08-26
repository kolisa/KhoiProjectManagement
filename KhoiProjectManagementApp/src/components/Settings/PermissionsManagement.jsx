// src/components/Settings/PermissionsManagement.js
// Admin UI for RolesController's role->permission mapping (backend already existed - this was the
// missing frontend). Gated by users.manage_roles, same permission the API endpoints require.
import React, { useState, useEffect } from 'react';
import { ShieldCheck, Plus, X } from 'lucide-react';
import { useToast } from '../../contexts/ToastContext';
import { reportApiError } from '../../utils/apiError';
import { validateRole, hasErrors } from '../../utils/validation';

const NewRoleModal = ({ onSave, onClose }) => {
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState(null);

  const handleSave = async () => {
    const validationErrors = validateRole({ name, description });
    if (hasErrors(validationErrors)) {
      setError(Object.values(validationErrors)[0]);
      return;
    }

    setSaving(true);
    setError(null);
    try {
      await onSave({ name, description });
    } catch (err) {
      setError(err.message);
      setSaving(false);
    }
  };

  return (
    <div className="fixed inset-0 bg-gray-900/50 backdrop-blur-sm flex items-center justify-center p-4 z-50">
      <div className="bg-white rounded-2xl shadow-xl overflow-hidden w-full max-w-md">
        <div className="px-6 py-4 border-b border-gray-100 flex items-center justify-between">
          <h3 className="text-base font-semibold text-gray-900">New Role</h3>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600" aria-label="Close">
            <X className="h-4 w-4" />
          </button>
        </div>
        <div className="px-6 py-5 space-y-4">
          {error && <div className="text-red-600 text-sm">{error}</div>}
          <input
            type="text"
            placeholder="Role name"
            value={name}
            onChange={(e) => setName(e.target.value)}
            className="w-full border border-gray-300 rounded-[10px] px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow"
            autoFocus
          />
          <input
            type="text"
            placeholder="Description (optional)"
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            className="w-full border border-gray-300 rounded-[10px] px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow"
          />
        </div>
        <div className="px-6 py-4 border-t border-gray-100 flex justify-end gap-3">
          <button onClick={onClose} className="inline-flex items-center gap-2 bg-white text-gray-700 border border-gray-300 px-4 py-2.5 rounded-[10px] text-sm font-semibold hover:bg-gray-50 transition-colors">Cancel</button>
          <button
            onClick={handleSave}
            disabled={saving || !name.trim()}
            className="inline-flex items-center gap-2 bg-blue-600 text-white px-4 py-2.5 rounded-[10px] text-sm font-semibold hover:bg-blue-700 shadow-sm transition-colors disabled:opacity-50"
          >
            {saving ? 'Creating...' : 'Create'}
          </button>
        </div>
      </div>
    </div>
  );
};

const PermissionsManagement = ({ apiService }) => {
  const toast = useToast();
  const [roles, setRoles] = useState(null);
  const [allPermissions, setAllPermissions] = useState(null);
  const [selectedRoleId, setSelectedRoleId] = useState(null);
  const [checkedPermissions, setCheckedPermissions] = useState(new Set());
  const [loadingRolePerms, setLoadingRolePerms] = useState(false);
  const [saving, setSaving] = useState(false);
  const [showNewRole, setShowNewRole] = useState(false);
  const [dirty, setDirty] = useState(false);

  const load = async () => {
    try {
      const [rolesResult, permsResult] = await Promise.all([
        apiService.getRoles(),
        apiService.getAllPermissions(),
      ]);
      setRoles(rolesResult || []);
      setAllPermissions(permsResult || []);
      if (!selectedRoleId && rolesResult?.length > 0) setSelectedRoleId(rolesResult[0].id);
    } catch (err) {
      reportApiError(toast, err, 'Could not load roles and permissions.');
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  };

  useEffect(() => {
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    if (!selectedRoleId) return;
    const loadRolePermissions = async () => {
      setLoadingRolePerms(true);
      try {
        const names = await apiService.getRolePermissions(selectedRoleId);
        setCheckedPermissions(new Set(names || []));
        setDirty(false);
      } catch (err) {
        reportApiError(toast, err, 'Could not load this role\'s permissions.');
      } finally {
        setLoadingRolePerms(false);
      }
    };
    loadRolePermissions();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selectedRoleId]);

  const selectedRole = roles?.find((r) => r.id === selectedRoleId) || null;

  const togglePermission = (name) => {
    setCheckedPermissions((prev) => {
      const next = new Set(prev);
      if (next.has(name)) next.delete(name);
      else next.add(name);
      return next;
    });
    setDirty(true);
  };

  const handleSave = async () => {
    setSaving(true);
    try {
      await apiService.setRolePermissions(selectedRoleId, Array.from(checkedPermissions));
      setDirty(false);
      toast.success(`Permissions updated for ${selectedRole?.name}.`);
    } catch (err) {
      reportApiError(toast, err, 'Could not save permissions.');
    } finally {
      setSaving(false);
    }
  };

  const handleCreateRole = async (dto) => {
    const created = await apiService.createRole(dto);
    setShowNewRole(false);
    await load();
    setSelectedRoleId(created.id);
    toast.success('Role created.');
  };

  const groupedPermissions = React.useMemo(() => {
    if (!allPermissions) return [];
    const byResource = {};
    allPermissions.forEach((p) => {
      if (!byResource[p.resource]) byResource[p.resource] = [];
      byResource[p.resource].push(p);
    });
    return Object.entries(byResource).sort(([a], [b]) => a.localeCompare(b));
  }, [allPermissions]);

  if (!roles || !allPermissions) {
    return <div className="text-gray-400 text-sm">Loading roles and permissions...</div>;
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h3 className="text-lg font-semibold text-gray-900 flex items-center">
            <ShieldCheck className="h-5 w-5 mr-2 text-gray-700" />
            Roles &amp; Permissions
          </h3>
          <p className="text-sm text-gray-500">Choose what each role is allowed to do across the app.</p>
        </div>
        <button
          onClick={() => setShowNewRole(true)}
          className="inline-flex items-center gap-2 bg-blue-600 text-white px-3.5 py-2 rounded-[10px] text-sm font-semibold hover:bg-blue-700 shadow-sm transition-colors"
        >
          <Plus className="h-4 w-4" />
          New Role
        </button>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-4 gap-4 max-w-4xl">
        <div className="md:col-span-1 bg-white rounded-2xl border border-gray-100 shadow-sm divide-y divide-gray-100 overflow-hidden">
          {roles.map((role) => (
            <button
              key={role.id}
              onClick={() => setSelectedRoleId(role.id)}
              className={`w-full text-left px-4 py-3 transition-colors ${selectedRoleId === role.id ? 'bg-blue-50' : 'hover:bg-gray-50/60'}`}
            >
              <div className="text-sm font-medium text-gray-900">{role.name}</div>
              {role.description && <div className="text-xs text-gray-500 mt-0.5">{role.description}</div>}
            </button>
          ))}
        </div>

        <div className="md:col-span-3 bg-white rounded-2xl border border-gray-100 shadow-sm p-5">
          {!selectedRole ? (
            <div className="text-gray-400 text-sm">Select a role to edit its permissions.</div>
          ) : loadingRolePerms ? (
            <div className="text-gray-400 text-sm">Loading permissions...</div>
          ) : (
            <>
              <div className="flex items-center justify-between mb-4">
                <div>
                  <div className="font-semibold text-gray-900">{selectedRole.name}</div>
                  {selectedRole.isSystemRole && (
                    <div className="text-xs text-gray-400">Built-in role - permissions can be changed, name cannot.</div>
                  )}
                </div>
                <button
                  onClick={handleSave}
                  disabled={saving || !dirty}
                  className="inline-flex items-center gap-2 bg-blue-600 text-white px-4 py-2 rounded-[10px] text-sm font-semibold hover:bg-blue-700 shadow-sm transition-colors disabled:opacity-50"
                >
                  {saving ? 'Saving...' : 'Save changes'}
                </button>
              </div>

              <div className="space-y-5 max-h-[28rem] overflow-y-auto pr-1">
                {groupedPermissions.map(([resource, perms]) => (
                  <div key={resource}>
                    <div className="text-xs font-semibold uppercase tracking-wider text-gray-400 mb-2">{resource}</div>
                    <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
                      {perms.map((perm) => (
                        <label key={perm.id} className="flex items-start gap-2 text-sm cursor-pointer">
                          <input
                            type="checkbox"
                            checked={checkedPermissions.has(perm.name)}
                            onChange={() => togglePermission(perm.name)}
                            className="mt-0.5 rounded border-gray-300 text-blue-600 focus:ring-blue-500"
                          />
                          <span>
                            <span className="text-gray-900 capitalize">{perm.action}</span>
                            {perm.description && <span className="block text-xs text-gray-400">{perm.description}</span>}
                          </span>
                        </label>
                      ))}
                    </div>
                  </div>
                ))}
              </div>
            </>
          )}
        </div>
      </div>

      {showNewRole && (
        <NewRoleModal onSave={handleCreateRole} onClose={() => setShowNewRole(false)} />
      )}
    </div>
  );
};

export default PermissionsManagement;
