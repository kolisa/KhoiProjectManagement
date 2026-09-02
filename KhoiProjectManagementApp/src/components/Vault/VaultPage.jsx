// src/components/Vault/VaultPage.js
import React, { useState, useEffect } from 'react';
import { Plus, Lock, FolderPlus, X, Users, Trash2, Upload } from 'lucide-react';
import SpaceTree from '../Spaces/SpaceTree';
import VaultEntryDetail from './VaultEntryDetail';
import VaultEntryModal from './VaultEntryModal';
import VaultImportModal from './VaultImportModal';
import ManageAccessModal from '../Spaces/ManageAccessModal';
import { hasSpaceLevel } from '../../utils/spaceLevel';
import { hasPermission } from '../../utils/permissions';
import { useToast } from '../../contexts/ToastContext';
import { useConfirm } from '../../contexts/ConfirmContext';
import { reportApiError } from '../../utils/apiError';
import useModalA11y from '../Common/useModalA11y';

// Extracted so useModalA11y's mount-time focus-trap setup runs exactly when this modal actually
// appears - VaultPage itself never unmounts while the Vault tab is open, so a hook call placed
// directly in VaultPage's body would run its one-time setup effect before showNewCategory ever
// flips true. Same JSX/classNames/handlers as before, just wrapped in a component that
// mounts/unmounts with the modal itself (the same shape VaultEntryModal/VaultImportModal use).
const NewVaultCategoryModal = ({ parentId, parentSpaceName, name, onNameChange, creating, error, onCreate, onClose }) => {
  const modalRef = useModalA11y(onClose);
  return (
    <div className="fixed inset-0 bg-gray-900/50 backdrop-blur-sm flex items-center justify-center p-4 z-50">
      <div ref={modalRef} role="dialog" aria-modal="true" aria-labelledby="vault-new-category-modal-title" tabIndex={-1} className="bg-white rounded-2xl shadow-xl w-full max-w-sm overflow-hidden outline-none">
        <div className="px-6 py-4 border-b border-gray-100 flex items-center justify-between">
          <h3 id="vault-new-category-modal-title" className="text-base font-semibold text-gray-900">
            {parentId ? `New subcategory under "${parentSpaceName}"` : 'New category'}
          </h3>
          <button
            onClick={onClose}
            className="text-gray-400 hover:bg-gray-100 rounded-md p-1.5 transition-colors"
            aria-label="Close"
          >
            <X className="h-4 w-4" />
          </button>
        </div>
        <div className="px-6 py-5 space-y-4">
          <p className="text-sm text-gray-500">
            {parentId
              ? 'Creates a category nested under the currently selected category.'
              : 'Creates a new top-level category, visible in the tree.'}
          </p>
          <input
            type="text"
            autoFocus
            value={name}
            onChange={(e) => onNameChange(e.target.value)}
            onKeyDown={(e) => { if (e.key === 'Enter') onCreate(); }}
            placeholder="Category name"
            className="w-full border border-gray-300 rounded-[10px] px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow"
          />
          {error && <div className="text-red-600 text-sm">{error}</div>}
        </div>
        <div className="px-6 py-4 border-t border-gray-100 flex justify-end gap-3">
          <button
            onClick={onClose}
            className="inline-flex items-center gap-2 bg-white text-gray-700 border border-gray-300 px-4 py-2.5 rounded-[10px] text-sm font-semibold hover:bg-gray-50 transition-colors"
          >
            Cancel
          </button>
          <button
            onClick={onCreate}
            disabled={creating || !name.trim()}
            className="inline-flex items-center gap-2 bg-blue-600 text-white px-4 py-2.5 rounded-[10px] text-sm font-semibold hover:bg-blue-700 shadow-sm transition-colors disabled:opacity-50"
          >
            {creating ? 'Creating...' : 'Create'}
          </button>
        </div>
      </div>
    </div>
  );
};

const VaultPage = ({ apiService, user, teamMembers = [] }) => {
  const toast = useToast();
  const confirm = useConfirm();
  const [selectedSpace, setSelectedSpace] = useState(null);
  const [entries, setEntries] = useState([]);
  const [loadingEntries, setLoadingEntries] = useState(false);
  const [error, setError] = useState(null);
  const [selectedEntryId, setSelectedEntryId] = useState(null);
  const [showModal, setShowModal] = useState(false);
  const [editingEntry, setEditingEntry] = useState(null);
  const [granteeCount, setGranteeCount] = useState(null);
  const [treeKey, setTreeKey] = useState(0);
  const [showNewCategory, setShowNewCategory] = useState(false);
  // Set explicitly by whichever button opened the modal (null = root) - NOT derived from
  // selectedSpace at submit time. SpaceTree auto-selects the first root category the moment one
  // exists (see its own "never rest on an empty placeholder" comment), so after creating the very
  // first category it was always selected already - every subsequent click on a selection-relative
  // "New category" button silently created a *subcategory* of it instead of a second root category,
  // with no way back out. Two separate, explicit entry points fixes it: this one for "new root
  // category" (always root, wherever it's clicked from) and a second one inside the selected
  // category's own header for "new subcategory here" (see canManage button below).
  const [newCategoryParentId, setNewCategoryParentId] = useState(null);
  const [newCategoryName, setNewCategoryName] = useState('');
  const [creatingCategory, setCreatingCategory] = useState(false);
  const [categoryError, setCategoryError] = useState(null);
  const [showManageAccess, setShowManageAccess] = useState(false);
  const [showImport, setShowImport] = useState(false);

  const loadEntries = async (spaceId) => {
    setLoadingEntries(true);
    try {
      const result = await apiService.getVaultEntries(spaceId);
      setEntries(result || []);
      setError(null);
    } catch (err) {
      setError(err.message);
    } finally {
      setLoadingEntries(false);
    }
  };

  useEffect(() => {
    if (selectedSpace) {
      loadEntries(selectedSpace.id);
      setSelectedEntryId(null);
      setGranteeCount(null);
      apiService.getSpaceGranteeCount(selectedSpace.id).then(setGranteeCount).catch(() => {});
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selectedSpace]);

  const handleSelectSpace = (space) => {
    setSelectedSpace(space);
  };

  const handleSaveEntry = async (data) => {
    const isEditing = !!editingEntry;
    if (isEditing) {
      await apiService.updateVaultEntry(editingEntry.id, data);
    } else {
      await apiService.createVaultEntry(data);
    }
    setShowModal(false);
    setEditingEntry(null);
    await loadEntries(selectedSpace.id);
    toast.success(isEditing ? 'Vault entry updated.' : 'Vault entry added.');
  };

  const canWrite = selectedSpace && hasSpaceLevel(selectedSpace.myEffectiveLevel, 'Write');
  const canManage = selectedSpace && hasSpaceLevel(selectedSpace.myEffectiveLevel, 'Manage');

  // Creating a root category needs spaces.manage (matches SpacesController.CreateSpace's rule for a
  // parentless Space); creating a subcategory just needs Manage on the currently selected category -
  // same rule Library's "New folder" already uses, since categories/folders are both just Spaces.
  const canCreateRootCategory = hasPermission(user?.permissions, 'spaces.manage');

  const openNewCategory = (parentId) => {
    setNewCategoryParentId(parentId);
    setCategoryError(null);
    setShowNewCategory(true);
  };

  const handleCreateCategory = async () => {
    if (!newCategoryName.trim()) return;
    setCreatingCategory(true);
    setCategoryError(null);
    try {
      await apiService.createSpace({
        name: newCategoryName.trim(),
        description: '',
        parentSpaceId: newCategoryParentId,
        spaceType: 'Generic',
        inheritPermissions: true,
      });
      setShowNewCategory(false);
      setNewCategoryName('');
      setTreeKey((k) => k + 1);
      toast.success('Category created.');
    } catch (err) {
      setCategoryError(err.message);
    } finally {
      setCreatingCategory(false);
    }
  };

  const handleDeleteCategory = async () => {
    if (!(await confirm(`Delete "${selectedSpace.name}"? This only works if it's empty.`, { title: 'Delete category', confirmText: 'Delete', danger: true }))) return;
    try {
      await apiService.deleteSpace(selectedSpace.id);
      setSelectedSpace(null);
      setTreeKey((k) => k + 1);
      toast.success('Category deleted.');
    } catch (err) {
      reportApiError(toast, err, 'Could not delete this category.');
    }
  };

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-[27px] font-bold text-gray-900 flex items-center">
          <Lock className="h-7 w-7 mr-2 text-gray-700" />
          Vault
        </h2>
        <p className="text-gray-600">Credentials and secrets, organized by category</p>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-4 gap-6">
        <div className="md:col-span-1 bg-white rounded-2xl border border-gray-100 shadow-sm p-3">
          <div className="flex justify-between items-center mb-2 px-1">
            <span className="text-xs font-semibold text-gray-500 uppercase">Categories</span>
            {canCreateRootCategory && (
              <button
                onClick={() => openNewCategory(null)}
                className="text-gray-400 hover:bg-gray-100 rounded-md p-1.5 transition-colors"
                aria-label="New category"
                title="New root category"
              >
                <FolderPlus className="h-4 w-4" />
              </button>
            )}
          </div>
          <SpaceTree key={treeKey} apiService={apiService} selectedSpaceId={selectedSpace?.id} onSelect={handleSelectSpace} />
        </div>

        <div className="md:col-span-3 space-y-4">
          {!selectedSpace && (
            <div className="bg-white rounded-2xl border border-gray-100 shadow-sm p-8 text-center text-gray-400">
              Select a category on the left to view its entries.
            </div>
          )}

          {selectedSpace && (
            <>
              <div className="flex justify-between items-center">
                <div>
                  <h3 className="text-xl font-semibold text-gray-900">{selectedSpace.name}</h3>
                  <p className="text-sm text-gray-500">
                    {entries.length} entr{entries.length !== 1 ? 'ies' : 'y'}
                    {granteeCount !== null && granteeCount > 0 && ` · shared with ${granteeCount} ${granteeCount !== 1 ? 'people' : 'person'}`}
                  </p>
                </div>
                <div className="flex items-center gap-2">
                  {canManage && (
                    <button
                      onClick={handleDeleteCategory}
                      className="text-gray-400 hover:bg-gray-100 hover:text-red-600 rounded-md p-2 transition-colors"
                      aria-label="Delete category"
                      title="Delete this category (must be empty)"
                    >
                      <Trash2 className="h-4 w-4" />
                    </button>
                  )}
                  {canManage && (
                    <button
                      onClick={() => openNewCategory(selectedSpace.id)}
                      className="inline-flex items-center gap-2 bg-white text-gray-700 border border-gray-300 px-4 py-2.5 rounded-[10px] text-sm font-semibold hover:bg-gray-50 transition-colors"
                      title={`New subcategory under "${selectedSpace.name}"`}
                    >
                      <FolderPlus className="h-4 w-4" />
                      New subcategory
                    </button>
                  )}
                  {canManage && (
                    <button
                      onClick={() => setShowManageAccess(true)}
                      className="inline-flex items-center gap-2 bg-white text-gray-700 border border-gray-300 px-4 py-2.5 rounded-[10px] text-sm font-semibold hover:bg-gray-50 transition-colors"
                    >
                      <Users className="h-4 w-4" />
                      Manage access
                    </button>
                  )}
                  {canWrite && (
                    <button
                      onClick={() => setShowImport(true)}
                      className="inline-flex items-center gap-2 bg-white text-gray-700 border border-gray-300 px-4 py-2.5 rounded-[10px] text-sm font-semibold hover:bg-gray-50 transition-colors"
                    >
                      <Upload className="h-4 w-4" />
                      Import
                    </button>
                  )}
                  {canWrite && (
                    <button
                      onClick={() => { setEditingEntry(null); setShowModal(true); }}
                      className="inline-flex items-center gap-2 bg-blue-600 text-white px-4 py-2.5 rounded-[10px] text-sm font-semibold hover:bg-blue-700 shadow-sm transition-colors"
                    >
                      <Plus className="h-5 w-5" />
                      New Entry
                    </button>
                  )}
                </div>
              </div>

              {error && <div className="text-red-600 text-sm">{error}</div>}
              {loadingEntries && <div className="text-gray-400">Loading entries...</div>}

              {!loadingEntries && !selectedEntryId && (
                <div className="bg-white rounded-2xl border border-gray-100 shadow-sm divide-y divide-gray-100">
                  {entries.length === 0 && (
                    <div className="p-6 text-center text-gray-400">No entries in this category yet.</div>
                  )}
                  {entries.map((entry) => (
                    <div
                      key={entry.id}
                      onClick={() => setSelectedEntryId(entry.id)}
                      className="p-4 hover:bg-gray-50/60 transition-colors cursor-pointer flex justify-between items-center"
                    >
                      <div>
                        <div className="font-medium text-gray-900">{entry.name}</div>
                        <div className="text-sm text-gray-500">{entry.systemOrUrl} {entry.username && `· ${entry.username}`}</div>
                      </div>
                    </div>
                  ))}
                </div>
              )}

              {selectedEntryId && (
                <VaultEntryDetail
                  apiService={apiService}
                  entryId={selectedEntryId}
                  myEffectiveLevel={selectedSpace.myEffectiveLevel}
                  onClose={() => setSelectedEntryId(null)}
                  onEdit={(entry) => { setEditingEntry(entry); setShowModal(true); }}
                  onDeleted={() => { setSelectedEntryId(null); loadEntries(selectedSpace.id); }}
                />
              )}
            </>
          )}
        </div>
      </div>

      {showManageAccess && selectedSpace && (
        <ManageAccessModal
          apiService={apiService}
          space={selectedSpace}
          teamMembers={teamMembers}
          currentUser={user}
          onClose={() => {
            setShowManageAccess(false);
            apiService.getSpaceGranteeCount(selectedSpace.id).then(setGranteeCount).catch(() => {});
          }}
        />
      )}

      {showModal && (
        <VaultEntryModal
          spaceId={selectedSpace?.id}
          entry={editingEntry}
          onSave={handleSaveEntry}
          onClose={() => { setShowModal(false); setEditingEntry(null); }}
        />
      )}

      {showImport && selectedSpace && (
        <VaultImportModal
          apiService={apiService}
          spaceId={selectedSpace.id}
          onImported={() => loadEntries(selectedSpace.id)}
          onClose={() => setShowImport(false)}
        />
      )}

      {showNewCategory && (
        <NewVaultCategoryModal
          parentId={newCategoryParentId}
          parentSpaceName={selectedSpace?.name}
          name={newCategoryName}
          onNameChange={setNewCategoryName}
          creating={creatingCategory}
          error={categoryError}
          onCreate={handleCreateCategory}
          onClose={() => { setShowNewCategory(false); setNewCategoryName(''); }}
        />
      )}
    </div>
  );
};

export default VaultPage;
