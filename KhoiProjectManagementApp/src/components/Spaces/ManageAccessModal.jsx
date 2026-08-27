// src/components/Spaces/ManageAccessModal.js
// Grant/revoke access to a Space (Vault category, Wiki space, Library folder - all the same
// underlying container, see SpacesController). The backend has always supported this
// (GetSpacePermissions/SetSpacePermissions) but no page ever rendered a way to reach it - the
// grantee-count text elsewhere in Vault only ever showed a number, never let anyone change it.
import React, { useState, useEffect } from 'react';
import { X, Trash2, Plus } from 'lucide-react';
import { useToast } from '../../contexts/ToastContext';
import { reportApiError } from '../../utils/apiError';
import { hasPermission } from '../../utils/permissions';

const LEVELS = ['Read', 'Write', 'Manage'];

const ManageAccessModal = ({ apiService, space, teamMembers, currentUser, onClose }) => {
  const toast = useToast();
  const [grants, setGrants] = useState(null);
  const [roles, setRoles] = useState(null);
  const [groups, setGroups] = useState(null);
  const [saving, setSaving] = useState(false);
  const [granteeType, setGranteeType] = useState('user');
  const [granteeId, setGranteeId] = useState('');
  const [newLevel, setNewLevel] = useState('Read');

  // Groups are gated by the same permission that controls managing them - only someone who can
  // manage groups can grant one Space access, matching how only someone who can manage roles can
  // grant a Role Space access (canSeeRoles below).
  const canSeeRoles = hasPermission(currentUser?.permissions, 'users.manage_roles');
  const canSeeGroups = hasPermission(currentUser?.permissions, 'groups.manage');

  useEffect(() => {
    const load = async () => {
      try {
        const result = await apiService.getSpacePermissions(space.id);
        setGrants((result || []).map((g) => ({
          key: g.id,
          userId: g.userId,
          roleId: g.roleId,
          groupId: g.groupId,
          name: g.userName || g.roleName || g.groupName,
          level: g.level,
        })));
      } catch (err) {
        reportApiError(toast, err, 'Could not load access for this space.');
        onClose();
      }
      if (canSeeRoles) {
        try {
          setRoles(await apiService.getRoles());
        } catch {
          setRoles([]);
        }
      }
      if (canSeeGroups) {
        try {
          setGroups(await apiService.getGroups());
        } catch {
          setGroups([]);
        }
      }
    };
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [space.id]);

  const availableUsers = teamMembers.filter((m) => !grants?.some((g) => g.userId === m.id));
  const availableRoles = (roles || []).filter((r) => !grants?.some((g) => g.roleId === r.id));
  const availableGroups = (groups || []).filter((gr) => !grants?.some((g) => g.groupId === gr.id));

  const handleAdd = () => {
    if (!granteeId) return;
    if (granteeType === 'user') {
      const user = teamMembers.find((m) => m.id === Number(granteeId));
      if (!user) return;
      setGrants((prev) => [...prev, { key: `new-user-${user.id}`, userId: user.id, roleId: null, groupId: null, name: user.name, level: newLevel }]);
    } else if (granteeType === 'role') {
      const role = (roles || []).find((r) => r.id === Number(granteeId));
      if (!role) return;
      setGrants((prev) => [...prev, { key: `new-role-${role.id}`, userId: null, roleId: role.id, groupId: null, name: role.name, level: newLevel }]);
    } else {
      const group = (groups || []).find((gr) => gr.id === Number(granteeId));
      if (!group) return;
      setGrants((prev) => [...prev, { key: `new-group-${group.id}`, userId: null, roleId: null, groupId: group.id, name: group.name, level: newLevel }]);
    }
    setGranteeId('');
  };

  const handleRemove = (key) => {
    setGrants((prev) => prev.filter((g) => g.key !== key));
  };

  const handleLevelChange = (key, level) => {
    setGrants((prev) => prev.map((g) => (g.key === key ? { ...g, level } : g)));
  };

  const handleSave = async () => {
    setSaving(true);
    try {
      await apiService.setSpacePermissions(space.id, grants.map((g) => ({
        userId: g.userId,
        roleId: g.roleId,
        groupId: g.groupId,
        level: g.level,
      })));
      toast.success('Access updated.');
      onClose();
    } catch (err) {
      reportApiError(toast, err, 'Could not save access changes.');
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="fixed inset-0 bg-gray-900/50 backdrop-blur-sm flex items-center justify-center p-4 z-50">
      <div className="bg-white rounded-2xl shadow-xl overflow-hidden w-full max-w-lg max-h-[85vh] flex flex-col">
        <div className="px-6 py-4 border-b border-gray-100 flex items-center justify-between flex-shrink-0">
          <div>
            <h3 className="text-base font-semibold text-gray-900">Manage access</h3>
            <p className="text-xs text-gray-500">{space.name}</p>
          </div>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600" aria-label="Close">
            <X className="h-4 w-4" />
          </button>
        </div>

        <div className="px-6 py-5 space-y-4 overflow-y-auto">
          {grants === null ? (
            <div className="text-sm text-gray-400">Loading...</div>
          ) : (
            <>
              {grants.length === 0 && <div className="text-sm text-gray-400 italic">Nobody has explicit access yet.</div>}
              <div className="space-y-2">
                {grants.map((g) => (
                  <div key={g.key} className="flex items-center gap-2 text-sm">
                    <span className="flex-1 text-gray-900 truncate">
                      {g.name}
                      {g.roleId && <span className="ml-1.5 text-xs text-gray-400">(role)</span>}
                      {g.groupId && <span className="ml-1.5 text-xs text-gray-400">(group)</span>}
                    </span>
                    <select
                      value={g.level}
                      onChange={(e) => handleLevelChange(g.key, e.target.value)}
                      className="text-xs border border-gray-300 rounded-md px-2 py-1.5 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
                    >
                      {LEVELS.map((l) => <option key={l} value={l}>{l}</option>)}
                    </select>
                    <button onClick={() => handleRemove(g.key)} className="text-red-400 hover:text-red-600 p-1" aria-label={`Remove ${g.name}`}>
                      <Trash2 className="h-3.5 w-3.5" />
                    </button>
                  </div>
                ))}
              </div>

              <div className="border-t border-gray-100 pt-4 flex items-center gap-2">
                {(canSeeRoles || canSeeGroups) && (
                  <select
                    value={granteeType}
                    onChange={(e) => { setGranteeType(e.target.value); setGranteeId(''); }}
                    className="text-sm border border-gray-300 rounded-[10px] px-2 py-2 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
                  >
                    <option value="user">Person</option>
                    {canSeeRoles && <option value="role">Role</option>}
                    {canSeeGroups && <option value="group">Group</option>}
                  </select>
                )}
                <select
                  value={granteeId}
                  onChange={(e) => setGranteeId(e.target.value)}
                  className="flex-1 text-sm border border-gray-300 rounded-[10px] px-2.5 py-2 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
                >
                  <option value="">
                    {granteeType === 'user' ? 'Add a person...' : granteeType === 'role' ? 'Add a role...' : 'Add a group...'}
                  </option>
                  {(granteeType === 'user' ? availableUsers : granteeType === 'role' ? availableRoles : availableGroups).map((item) => (
                    <option key={item.id} value={item.id}>{item.name}</option>
                  ))}
                </select>
                <select
                  value={newLevel}
                  onChange={(e) => setNewLevel(e.target.value)}
                  className="text-sm border border-gray-300 rounded-[10px] px-2 py-2 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
                >
                  {LEVELS.map((l) => <option key={l} value={l}>{l}</option>)}
                </select>
                <button
                  onClick={handleAdd}
                  disabled={!granteeId}
                  className="text-blue-600 hover:text-blue-800 disabled:opacity-40 p-2"
                  aria-label="Add"
                >
                  <Plus className="h-4 w-4" />
                </button>
              </div>
            </>
          )}
        </div>

        <div className="px-6 py-4 border-t border-gray-100 flex justify-end gap-3 flex-shrink-0">
          <button onClick={onClose} className="inline-flex items-center gap-2 bg-white text-gray-700 border border-gray-300 px-4 py-2.5 rounded-[10px] text-sm font-semibold hover:bg-gray-50 transition-colors">
            Cancel
          </button>
          <button
            onClick={handleSave}
            disabled={saving || grants === null}
            className="inline-flex items-center gap-2 bg-blue-600 text-white px-4 py-2.5 rounded-[10px] text-sm font-semibold hover:bg-blue-700 shadow-sm transition-colors disabled:opacity-50"
          >
            {saving ? 'Saving...' : 'Save'}
          </button>
        </div>
      </div>
    </div>
  );
};

export default ManageAccessModal;
