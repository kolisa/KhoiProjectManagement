// src/components/Library/LibraryPage.js
import React, { useState, useEffect, useRef, useCallback } from 'react';
import { Upload, FolderOpen, FolderPlus, Download, Trash2, History, Plus, X, Users, Eye } from 'lucide-react';
import SpaceTree from '../Spaces/SpaceTree';
import LibraryVersionHistory from './LibraryVersionHistory';
import ManageAccessModal from '../Spaces/ManageAccessModal';
import { hasSpaceLevel } from '../../utils/spaceLevel';
import { hasPermission } from '../../utils/permissions';
import { formatFileSize } from '../../utils/formatFileSize';
import ShareButton from '../Common/ShareButton';
import LoadingSpinner from '../Common/LoadingSpinner';
import ErrorMessage from '../Common/ErrorMessage';
import useModalA11y from '../Common/useModalA11y';
import { useToast } from '../../contexts/ToastContext';
import { reportApiError } from '../../utils/apiError';

const LibraryPage = ({ apiService, user, teamMembers = [], deepLink }) => {
  const toast = useToast();
  const [selectedSpace, setSelectedSpace] = useState(null);
  const [files, setFiles] = useState([]);
  const [loadingFiles, setLoadingFiles] = useState(false);
  const [error, setError] = useState(null);
  const [expandedFileId, setExpandedFileId] = useState(null);
  const [uploading, setUploading] = useState(false);
  const [treeKey, setTreeKey] = useState(0);
  const [showNewFolder, setShowNewFolder] = useState(false);
  // Explicit target for the folder about to be created, set only when the modal is opened - NOT
  // derived from selectedSpace at submit time. SpaceTree auto-selects the first root folder the
  // moment it loads, so a submit-time `selectedSpace?.id ?? null` silently turns every "New root
  // folder" click into "new subfolder under whatever's currently selected" once any root folder
  // exists, making a second root folder unreachable through the UI. Two separate entry points below
  // (the tree-header "+" always passes null; "New subfolder" passes selectedSpace.id) remove the
  // ambiguity instead of trying to infer intent from selection state.
  const [newFolderParentId, setNewFolderParentId] = useState(null);
  const [newFolderName, setNewFolderName] = useState('');
  const [creatingFolder, setCreatingFolder] = useState(false);
  const [showManageAccess, setShowManageAccess] = useState(false);
  const uploadInputRef = useRef(null);
  const versionInputRef = useRef(null);
  const versionTargetIdRef = useRef(null);
  const newFolderModalRef = useModalA11y(() => { setShowNewFolder(false); setNewFolderName(''); });

  const loadFiles = async (spaceId) => {
    setLoadingFiles(true);
    try {
      const result = await apiService.getLibraryFiles(spaceId);
      setFiles(result || []);
      setError(null);
    } catch (err) {
      setError(err.message);
    } finally {
      setLoadingFiles(false);
    }
  };

  // Only loads files here - expandedFileId is reset by handleSelectSpace (genuine user navigation),
  // never as a blanket side effect of selectedSpace changing. See WikiPage.js for why: a ref-timing
  // approach here previously broke under React 18 StrictMode's dev-mode double effect invocation,
  // which replayed the "reset to null" branch after a deep link had already set expandedFileId.
  useEffect(() => {
    if (selectedSpace) {
      loadFiles(selectedSpace.id);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selectedSpace]);

  const handleSelectSpace = useCallback((space) => {
    setSelectedSpace(space);
    setExpandedFileId(null);
  }, []);

  // A shared link jumps straight to the linked folder - if the recipient doesn't have Read access,
  // getSpace fails exactly the same way normal browsing would (a shortcut, not a bypass).
  useEffect(() => {
    if (!deepLink?.spaceId) return;
    (async () => {
      try {
        const space = await apiService.getSpace(Number(deepLink.spaceId));
        setSelectedSpace(space);
        if (deepLink.fileId) setExpandedFileId(Number(deepLink.fileId));
      } catch (err) {
        setError(err.message);
      }
    })();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [deepLink]);

  const handleUploadNew = async (e) => {
    const file = e.target.files?.[0];
    e.target.value = '';
    if (!file || !selectedSpace) return;
    setUploading(true);
    try {
      await apiService.uploadLibraryFile(selectedSpace.id, file);
      await loadFiles(selectedSpace.id);
      toast.success(`"${file.name}" uploaded.`);
    } catch (err) {
      reportApiError(toast, err, 'Could not upload this file.');
    } finally {
      setUploading(false);
    }
  };

  const handleUploadVersion = async (e) => {
    const file = e.target.files?.[0];
    const fileId = versionTargetIdRef.current;
    e.target.value = '';
    if (!file || !fileId) return;
    setUploading(true);
    try {
      await apiService.uploadLibraryFileVersion(fileId, file);
      await loadFiles(selectedSpace.id);
      toast.success('New version uploaded.');
    } catch (err) {
      reportApiError(toast, err, 'Could not upload this version.');
    } finally {
      setUploading(false);
    }
  };

  const handleView = async (file) => {
    try {
      await apiService.viewLibraryFile(file.id);
    } catch (err) {
      reportApiError(toast, err, 'Could not open this file.');
    }
  };

  const handleDownload = async (file) => {
    try {
      await apiService.downloadLibraryFile(file.id, file.fileName);
    } catch (err) {
      reportApiError(toast, err, 'Could not download this file.');
    }
  };

  const handleDelete = async (file) => {
    if (!window.confirm(`Delete "${file.fileName}"?`)) return;
    try {
      await apiService.deleteLibraryFile(file.id);
      await loadFiles(selectedSpace.id);
      toast.success('File deleted.');
    } catch (err) {
      reportApiError(toast, err, 'Could not delete this file.');
    }
  };

  const canWrite = selectedSpace && hasSpaceLevel(selectedSpace.myEffectiveLevel, 'Write');
  const canManage = selectedSpace && hasSpaceLevel(selectedSpace.myEffectiveLevel, 'Manage');

  // Creating a root folder needs spaces.manage (matches SpacesController.CreateSpace's rule for a
  // parentless Space); creating a subfolder just needs Manage on the currently selected folder.
  const canCreateRootFolder = hasPermission(user?.permissions, 'spaces.manage');

  const openNewFolder = (parentId) => {
    setNewFolderParentId(parentId);
    setError(null);
    setShowNewFolder(true);
  };

  const handleCreateFolder = async () => {
    if (!newFolderName.trim()) return;
    setCreatingFolder(true);
    try {
      await apiService.createSpace({
        name: newFolderName.trim(),
        description: '',
        parentSpaceId: newFolderParentId,
        spaceType: 'Generic',
        inheritPermissions: true,
      });
      setShowNewFolder(false);
      setNewFolderName('');
      setTreeKey((k) => k + 1);
      toast.success('Folder created.');
    } catch (err) {
      setError(err.message);
    } finally {
      setCreatingFolder(false);
    }
  };

  const handleDeleteFolder = async () => {
    if (!window.confirm(`Delete "${selectedSpace.name}"? This only works if it's empty.`)) return;
    try {
      await apiService.deleteSpace(selectedSpace.id);
      setSelectedSpace(null);
      setTreeKey((k) => k + 1);
      toast.success('Folder deleted.');
    } catch (err) {
      reportApiError(toast, err, 'Could not delete this folder.');
    }
  };

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-[27px] font-bold text-gray-900 flex items-center">
          <FolderOpen className="h-7 w-7 mr-2 text-gray-700" />
          File Library
        </h2>
        <p className="text-gray-600">Shared files, organized by folder, with version history</p>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-4 gap-6">
        <div className="md:col-span-1 bg-white rounded-2xl border border-gray-100 shadow-sm p-3">
          <div className="flex justify-between items-center mb-2 px-1">
            <span className="text-xs font-semibold text-gray-500 uppercase">Folders</span>
            {canCreateRootFolder && (
              <button
                onClick={() => openNewFolder(null)}
                className="text-gray-400 hover:bg-gray-100 rounded-md p-1.5 transition-colors"
                aria-label="New root folder"
                title="New root folder"
              >
                <FolderPlus className="h-4 w-4" />
              </button>
            )}
          </div>
          <SpaceTree key={treeKey} apiService={apiService} selectedSpaceId={selectedSpace?.id} onSelect={handleSelectSpace} />
        </div>

        <div className="md:col-span-3 space-y-4">
          {!selectedSpace && (
            <div className="bg-white rounded-2xl border border-gray-100 shadow-sm p-10 text-center text-gray-400">
              <FolderOpen className="h-9 w-9 mx-auto mb-2 text-gray-300" />
              Select a folder on the left to view its files.
            </div>
          )}

          {selectedSpace && (
            <>
              <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                <div className="min-w-0">
                  <h3 className="text-xl font-semibold text-gray-900 truncate" title={selectedSpace.name}>{selectedSpace.name}</h3>
                  <p className="text-sm text-gray-500">{files.length} file{files.length !== 1 ? 's' : ''}</p>
                </div>
                <div className="flex flex-wrap items-center gap-2">
                  {canManage && (
                    <button
                      onClick={handleDeleteFolder}
                      className="text-gray-400 hover:bg-gray-100 hover:text-red-600 rounded-md p-2 transition-colors"
                      aria-label="Delete folder"
                      title="Delete this folder (must be empty)"
                    >
                      <Trash2 className="h-4 w-4" />
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
                  {canManage && (
                    <button
                      onClick={() => openNewFolder(selectedSpace.id)}
                      className="inline-flex items-center gap-2 bg-white text-gray-700 border border-gray-300 px-4 py-2.5 rounded-[10px] text-sm font-semibold hover:bg-gray-50 transition-colors"
                      title={`New subfolder under "${selectedSpace.name}"`}
                    >
                      <FolderPlus className="h-4 w-4" />
                      New subfolder
                    </button>
                  )}
                  {canWrite && (
                    <button
                      onClick={() => uploadInputRef.current?.click()}
                      disabled={uploading}
                      className="inline-flex items-center gap-2 bg-blue-600 text-white px-4 py-2.5 rounded-[10px] text-sm font-semibold hover:bg-blue-700 shadow-sm transition-colors disabled:opacity-50"
                    >
                      <Upload className="h-5 w-5" />
                      {uploading ? 'Uploading...' : 'Upload File'}
                    </button>
                  )}
                </div>
                {canWrite && (
                  <>
                    <input ref={uploadInputRef} type="file" className="hidden" onChange={handleUploadNew} />
                    <input ref={versionInputRef} type="file" className="hidden" onChange={handleUploadVersion} />
                  </>
                )}
              </div>

              {error && <ErrorMessage message={error} />}
              {loadingFiles && <LoadingSpinner text="Loading files..." />}

              {!loadingFiles && (
                <div className="bg-white rounded-2xl border border-gray-100 shadow-sm divide-y divide-gray-100">
                  {files.length === 0 && (
                    <div className="p-8 text-center text-gray-400">
                      <Upload className="h-8 w-8 mx-auto mb-2 text-gray-300" />
                      No files in this folder yet.
                    </div>
                  )}
                  {files.map((file) => (
                    <div key={file.id} className="hover:bg-gray-50/60 transition-colors">
                      <div className="p-4 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                        <div className="min-w-0">
                          <div className="font-medium text-gray-900 truncate" title={file.fileName}>{file.fileName}</div>
                          <div className="text-sm text-gray-500">
                            v{file.currentVersionNumber} · {formatFileSize(file.fileSize)} · {file.creatorName}
                          </div>
                        </div>
                        <div className="flex flex-wrap items-center gap-2 sm:gap-3 sm:flex-shrink-0">
                          <button
                            onClick={() => handleView(file)}
                            className="text-gray-400 hover:bg-gray-100 rounded-md p-1.5 transition-colors"
                            aria-label="View"
                            title="View in a new tab"
                          >
                            <Eye className="h-4 w-4" />
                          </button>
                          <button
                            onClick={() => handleDownload(file)}
                            className="text-blue-600 hover:bg-gray-100 rounded-md p-1.5 transition-colors"
                            aria-label="Download"
                            title="Download"
                          >
                            <Download className="h-4 w-4" />
                          </button>
                          <button
                            onClick={() => setExpandedFileId(expandedFileId === file.id ? null : file.id)}
                            className="text-gray-400 hover:bg-gray-100 rounded-md p-1.5 transition-colors"
                            aria-label="Version history"
                            title="Version history"
                          >
                            <History className="h-4 w-4" />
                          </button>
                          <ShareButton
                            url={`${window.location.origin}${window.location.pathname}?tab=library&spaceId=${selectedSpace.id}&fileId=${file.id}`}
                            label={file.fileName}
                          />
                          {canWrite && (
                            <button
                              onClick={() => { versionTargetIdRef.current = file.id; versionInputRef.current?.click(); }}
                              className="text-gray-400 hover:bg-gray-100 rounded-md p-1.5 transition-colors"
                              aria-label="Upload new version"
                              title="Upload new version"
                            >
                              <Plus className="h-4 w-4" />
                            </button>
                          )}
                          {canManage && (
                            <button
                              onClick={() => handleDelete(file)}
                              className="text-red-400 hover:bg-gray-100 rounded-md p-1.5 transition-colors"
                              aria-label="Delete"
                              title="Delete"
                            >
                              <Trash2 className="h-4 w-4" />
                            </button>
                          )}
                        </div>
                      </div>
                      {expandedFileId === file.id && (
                        <div className="px-4 pb-4">
                          <LibraryVersionHistory apiService={apiService} file={file} />
                        </div>
                      )}
                    </div>
                  ))}
                </div>
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
          onClose={() => setShowManageAccess(false)}
        />
      )}

      {showNewFolder && (
        <div className="fixed inset-0 bg-gray-900/50 backdrop-blur-sm flex items-center justify-center p-4 z-50">
          <div ref={newFolderModalRef} role="dialog" aria-modal="true" aria-labelledby="library-new-folder-modal-title" tabIndex={-1} className="bg-white rounded-2xl shadow-xl w-full max-w-sm overflow-hidden outline-none">
            <div className="px-6 py-4 border-b border-gray-100 flex items-center justify-between">
              <h3 id="library-new-folder-modal-title" className="text-base font-semibold text-gray-900">
                {newFolderParentId ? `New subfolder under "${selectedSpace?.name}"` : 'New root folder'}
              </h3>
              <button
                onClick={() => { setShowNewFolder(false); setNewFolderName(''); }}
                className="text-gray-400 hover:bg-gray-100 rounded-md p-1.5 transition-colors"
                aria-label="Close"
              >
                <X className="h-4 w-4" />
              </button>
            </div>
            <div className="px-6 py-5 space-y-4">
              <p className="text-sm text-gray-500">
                {newFolderParentId
                  ? 'Creates a folder nested under the currently selected folder.'
                  : 'Creates a new top-level folder, visible in the tree.'}
              </p>
              <input
                type="text"
                autoFocus
                value={newFolderName}
                onChange={(e) => setNewFolderName(e.target.value)}
                onKeyDown={(e) => { if (e.key === 'Enter') handleCreateFolder(); }}
                placeholder="Folder name"
                className="w-full border border-gray-300 rounded-[10px] px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow"
              />
              {error && <div className="text-red-600 text-sm">{error}</div>}
            </div>
            <div className="px-6 py-4 border-t border-gray-100 flex justify-end gap-3">
              <button
                onClick={() => { setShowNewFolder(false); setNewFolderName(''); }}
                className="inline-flex items-center gap-2 bg-white text-gray-700 border border-gray-300 px-4 py-2.5 rounded-[10px] text-sm font-semibold hover:bg-gray-50 transition-colors"
              >
                Cancel
              </button>
              <button
                onClick={handleCreateFolder}
                disabled={creatingFolder || !newFolderName.trim()}
                className="inline-flex items-center gap-2 bg-blue-600 text-white px-4 py-2.5 rounded-[10px] text-sm font-semibold hover:bg-blue-700 shadow-sm transition-colors disabled:opacity-50"
              >
                {creatingFolder ? 'Creating...' : 'Create'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

export default LibraryPage;
