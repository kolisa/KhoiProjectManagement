// src/components/Settings/GroupsManagement.js
// Admin UI for GroupsController - ad-hoc, named collections of users that can then be granted Space
// access as a unit via ManageAccessModal's "Group" grantee type, the same way a Role can be. Mirrors
// PermissionsManagement.js's two-pane layout, swapping the permission-checkbox-grid for a member
// checkbox-list (same multi-select pattern as the Add/Edit Project "Team members" picker in App.js).
// Gated by groups.manage, same permission the API endpoints require.
import React, { useState, useEffect } from 'react';
import { Users, Plus, X, Pencil } from 'lucide-react';
import { useToast } from '../../contexts/ToastContext';
import { reportApiError } from '../../utils/apiError';
import { validateGroup, hasErrors } from '../../utils/validation';
import useModalA11y from '../Common/useModalA11y';

const NewGroupModal = ({ onSave, onClose }) => {
  const modalRef = useModalA11y(onClose);
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState(null);

  const handleSave = async () => {
    const validationErrors = validateGroup({ name, description });
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
      <div ref={modalRef} role="dialog" aria-modal="true" aria-labelledby="new-group-modal-title" tabIndex={-1} className="bg-white rounded-2xl shadow-xl overflow-hidden w-full max-w-md outline-none">
        <div className="px-6 py-4 border-b border-gray-100 flex items-center justify-between">
          <h3 id="new-group-modal-title" className="text-base font-semibold text-gray-900">New Group</h3>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600" aria-label="Close">
            <X className="h-4 w-4" />
          </button>
        </div>
        <div className="px-6 py-5 space-y-4">
          {error && <div className="text-red-600 text-sm">{error}</div>}
          <input
            type="text"
            placeholder="Group name"
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

const GroupsManagement = ({ apiService, teamMembers = [] }) => {
  const toast = useToast();
  const [groups, setGroups] = useState(null);
  const [selectedGroupId, setSelectedGroupId] = useState(null);
  const [memberIds, setMemberIds] = useState(new Set());
  const [loadingMembers, setLoadingMembers] = useState(false);
  const [saving, setSaving] = useState(false);
  const [showNewGroup, setShowNewGroup] = useState(false);
  const [dirty, setDirty] = useState(false);
  const [editingGroup, setEditingGroup] = useState(false);
  const [editName, setEditName] = useState('');
  const [editDescription, setEditDescription] = useState('');
  const [savingGroupInfo, setSavingGroupInfo] = useState(false);

  const load = async () => {
    try {
      const result = await apiService.getGroups();
      setGroups(result || []);
      if (!selectedGroupId && result?.length > 0) setSelectedGroupId(result[0].id);
    } catch (err) {
      reportApiError(toast, err, 'Could not load groups.');
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  };

  useEffect(() => {
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    setEditingGroup(false);
    if (!selectedGroupId) return;
    const loadMembers = async () => {
      setLoadingMembers(true);
      try {
        const ids = await apiService.getGroupMembers(selectedGroupId);
        setMemberIds(new Set(ids || []));
        setDirty(false);
      } catch (err) {
        reportApiError(toast, err, 'Could not load this group\'s members.');
      } finally {
        setLoadingMembers(false);
      }
    };
    loadMembers();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selectedGroupId]);

  const selectedGroup = groups?.find((g) => g.id === selectedGroupId) || null;

  const toggleMember = (userId) => {
    setMemberIds((prev) => {
      const next = new Set(prev);
      if (next.has(userId)) next.delete(userId);
      else next.add(userId);
      return next;
    });
    setDirty(true);
  };

  const handleSave = async () => {
    setSaving(true);
    try {
      await apiService.setGroupMembers(selectedGroupId, Array.from(memberIds));
      setDirty(false);
      await load();
      toast.success(`Members updated for ${selectedGroup?.name}.`);
    } catch (err) {
      reportApiError(toast, err, 'Could not save group members.');
    } finally {
      setSaving(false);
    }
  };

  const handleStartEditGroup = () => {
    setEditName(selectedGroup.name);
    setEditDescription(selectedGroup.description || '');
    setEditingGroup(true);
  };

  const handleSaveGroupInfo = async () => {
    const validationErrors = validateGroup({ name: editName, description: editDescription });
    if (hasErrors(validationErrors)) {
      toast.error(Object.values(validationErrors)[0]);
      return;
    }
    setSavingGroupInfo(true);
    try {
      await apiService.updateGroup(selectedGroupId, { name: editName, description: editDescription });
      setEditingGroup(false);
      await load();
      toast.success('Group updated.');
    } catch (err) {
      reportApiError(toast, err, 'Could not update this group.');
    } finally {
      setSavingGroupInfo(false);
    }
  };

  const handleCreateGroup = async (dto) => {
    const created = await apiService.createGroup(dto);
    setShowNewGroup(false);
    await load();
    setSelectedGroupId(created.id);
    toast.success('Group created.');
  };

  if (!groups) {
    return <div className="text-gray-400 text-sm">Loading groups...</div>;
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h3 className="text-lg font-semibold text-gray-900 flex items-center">
            <Users className="h-5 w-5 mr-2 text-gray-700" />
            Groups
          </h3>
          <p className="text-sm text-gray-500">Name a set of people once, then grant it Space access from Manage Access.</p>
        </div>
        <button
          onClick={() => setShowNewGroup(true)}
          className="inline-flex items-center gap-2 bg-blue-600 text-white px-3.5 py-2 rounded-[10px] text-sm font-semibold hover:bg-blue-700 shadow-sm transition-colors"
        >
          <Plus className="h-4 w-4" />
          New Group
        </button>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-4 gap-4 max-w-4xl">
        <div className="md:col-span-1 bg-white rounded-2xl border border-gray-100 shadow-sm divide-y divide-gray-100 overflow-hidden">
          {groups.length === 0 && (
            <div className="px-4 py-3 text-sm text-gray-400">No groups yet.</div>
          )}
          {groups.map((group) => (
            <button
              key={group.id}
              onClick={() => setSelectedGroupId(group.id)}
              className={`w-full text-left px-4 py-3 transition-colors ${selectedGroupId === group.id ? 'bg-blue-50' : 'hover:bg-gray-50/60'}`}
            >
              <div className="text-sm font-medium text-gray-900">{group.name}</div>
              {group.description && <div className="text-xs text-gray-500 mt-0.5">{group.description}</div>}
              <div className="text-xs text-gray-400 mt-0.5">{group.memberCount} member{group.memberCount !== 1 ? 's' : ''}</div>
            </button>
          ))}
        </div>

        <div className="md:col-span-3 bg-white rounded-2xl border border-gray-100 shadow-sm p-5">
          {!selectedGroup ? (
            <div className="text-gray-400 text-sm">Select a group to edit its members.</div>
          ) : loadingMembers ? (
            <div className="text-gray-400 text-sm">Loading members...</div>
          ) : (
            <>
              {editingGroup ? (
                <div className="mb-4 space-y-2 border border-gray-200 rounded-[10px] p-3">
                  <input
                    type="text"
                    value={editName}
                    onChange={(e) => setEditName(e.target.value)}
                    placeholder="Group name"
                    className="w-full border border-gray-300 rounded-md px-2.5 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
                  />
                  <input
                    type="text"
                    value={editDescription}
                    onChange={(e) => setEditDescription(e.target.value)}
                    placeholder="Description (optional)"
                    className="w-full border border-gray-300 rounded-md px-2.5 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
                  />
                  <div className="flex justify-end gap-2">
                    <button onClick={() => setEditingGroup(false)} className="text-xs font-semibold text-gray-600 hover:text-gray-800 px-2.5 py-1.5">Cancel</button>
                    <button
                      onClick={handleSaveGroupInfo}
                      disabled={savingGroupInfo}
                      className="text-xs font-semibold bg-blue-600 text-white px-2.5 py-1.5 rounded-md hover:bg-blue-700 disabled:opacity-50"
                    >
                      {savingGroupInfo ? 'Saving...' : 'Save'}
                    </button>
                  </div>
                </div>
              ) : (
                <div className="flex items-center justify-between mb-4">
                  <div className="flex items-center gap-2">
                    <div>
                      <div className="font-semibold text-gray-900">{selectedGroup.name}</div>
                      {selectedGroup.description && <div className="text-xs text-gray-500">{selectedGroup.description}</div>}
                    </div>
                    <button onClick={handleStartEditGroup} className="text-gray-400 hover:text-gray-600 p-1" aria-label="Edit group">
                      <Pencil className="h-3.5 w-3.5" />
                    </button>
                  </div>
                  <button
                    onClick={handleSave}
                    disabled={saving || !dirty}
                    className="inline-flex items-center gap-2 bg-blue-600 text-white px-4 py-2 rounded-[10px] text-sm font-semibold hover:bg-blue-700 shadow-sm transition-colors disabled:opacity-50"
                  >
                    {saving ? 'Saving...' : 'Save changes'}
                  </button>
                </div>
              )}

              <div className="border border-gray-200 rounded-[10px] max-h-80 overflow-y-auto divide-y divide-gray-100">
                {teamMembers.length === 0 ? (
                  <p className="px-3.5 py-2.5 text-sm text-gray-400">No team members yet.</p>
                ) : (
                  teamMembers.map((member) => (
                    <label key={member.id} className="flex items-center gap-2.5 px-3.5 py-2 text-sm cursor-pointer hover:bg-gray-50/60 transition-colors">
                      <input
                        type="checkbox"
                        checked={memberIds.has(member.id)}
                        onChange={() => toggleMember(member.id)}
                        className="rounded border-gray-300 text-blue-600 focus:ring-blue-500"
                      />
                      <span className="text-gray-900">{member.name}</span>
                      <span className="text-gray-400 text-xs">{member.position}</span>
                    </label>
                  ))
                )}
              </div>
            </>
          )}
        </div>
      </div>

      {showNewGroup && (
        <NewGroupModal onSave={handleCreateGroup} onClose={() => setShowNewGroup(false)} />
      )}
    </div>
  );
};

export default GroupsManagement;
