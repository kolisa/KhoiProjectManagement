// src/components/Vault/VaultPage.js
import React, { useState, useEffect } from 'react';
import { Plus, Lock } from 'lucide-react';
import SpaceTree from '../Spaces/SpaceTree';
import VaultEntryDetail from './VaultEntryDetail';
import VaultEntryModal from './VaultEntryModal';
import { hasSpaceLevel } from '../../utils/spaceLevel';

const VaultPage = ({ apiService }) => {
  const [selectedSpace, setSelectedSpace] = useState(null);
  const [entries, setEntries] = useState([]);
  const [loadingEntries, setLoadingEntries] = useState(false);
  const [error, setError] = useState(null);
  const [selectedEntryId, setSelectedEntryId] = useState(null);
  const [showModal, setShowModal] = useState(false);
  const [editingEntry, setEditingEntry] = useState(null);
  const [granteeCount, setGranteeCount] = useState(null);

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
    if (editingEntry) {
      await apiService.updateVaultEntry(editingEntry.id, data);
    } else {
      await apiService.createVaultEntry(data);
    }
    setShowModal(false);
    setEditingEntry(null);
    await loadEntries(selectedSpace.id);
  };

  const canWrite = selectedSpace && hasSpaceLevel(selectedSpace.myEffectiveLevel, 'Write');

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
          <SpaceTree apiService={apiService} selectedSpaceId={selectedSpace?.id} onSelect={handleSelectSpace} />
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

      {showModal && (
        <VaultEntryModal
          spaceId={selectedSpace?.id}
          entry={editingEntry}
          onSave={handleSaveEntry}
          onClose={() => { setShowModal(false); setEditingEntry(null); }}
        />
      )}
    </div>
  );
};

export default VaultPage;
