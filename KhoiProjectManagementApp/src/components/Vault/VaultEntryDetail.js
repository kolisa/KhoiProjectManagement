// src/components/Vault/VaultEntryDetail.js
import React, { useState, useEffect } from 'react';
import { Eye, EyeOff, Clock, Trash2, Edit3, X } from 'lucide-react';
import { hasSpaceLevel } from '../../utils/spaceLevel';

const VaultEntryDetail = ({ apiService, entryId, myEffectiveLevel, onClose, onEdit, onDeleted }) => {
  const [entry, setEntry] = useState(null);
  const [error, setError] = useState(null);
  const [secret, setSecret] = useState(null);
  const [revealing, setRevealing] = useState(false);
  const [auditLog, setAuditLog] = useState(null);
  const [showAudit, setShowAudit] = useState(false);

  useEffect(() => {
    const load = async () => {
      try {
        const result = await apiService.getVaultEntry(entryId);
        setEntry(result);
      } catch (err) {
        setError(err.message);
      }
    };
    load();
    setSecret(null);
  }, [apiService, entryId]);

  const handleReveal = async () => {
    setRevealing(true);
    try {
      const result = await apiService.revealVaultSecret(entryId);
      setSecret(result.secretValue);
    } catch (err) {
      setError(err.message);
    } finally {
      setRevealing(false);
    }
  };

  const handleShowAudit = async () => {
    if (!showAudit && auditLog === null) {
      try {
        const result = await apiService.getVaultAuditLog(entryId);
        setAuditLog(result || []);
      } catch (err) {
        setError(err.message);
      }
    }
    setShowAudit(!showAudit);
  };

  const handleDelete = async () => {
    if (!window.confirm('Delete this vault entry?')) return;
    try {
      await apiService.deleteVaultEntry(entryId);
      onDeleted();
    } catch (err) {
      setError(err.message);
    }
  };

  const canWrite = hasSpaceLevel(myEffectiveLevel, 'Write');
  const canManage = hasSpaceLevel(myEffectiveLevel, 'Manage');

  if (error) return <div className="p-4 text-red-600">Error: {error}</div>;
  if (!entry) return <div className="p-4 text-gray-400">Loading...</div>;

  return (
    <div className="bg-white rounded-lg shadow p-6">
      <div className="flex justify-between items-start mb-4">
        <h3 className="text-lg font-semibold text-gray-900">{entry.name}</h3>
        <div className="flex space-x-2">
          {canWrite && (
            <button onClick={() => onEdit(entry)} className="text-gray-400 hover:text-gray-600" aria-label="Edit">
              <Edit3 className="h-4 w-4" />
            </button>
          )}
          {canManage && (
            <button onClick={handleDelete} className="text-red-400 hover:text-red-600" aria-label="Delete">
              <Trash2 className="h-4 w-4" />
            </button>
          )}
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600" aria-label="Close">
            <X className="h-4 w-4" />
          </button>
        </div>
      </div>

      <dl className="space-y-2 text-sm">
        {entry.systemOrUrl && (
          <div>
            <dt className="text-gray-500">System / URL</dt>
            <dd className="text-gray-900">{entry.systemOrUrl}</dd>
          </div>
        )}
        {entry.username && (
          <div>
            <dt className="text-gray-500">Username</dt>
            <dd className="text-gray-900">{entry.username}</dd>
          </div>
        )}
        <div>
          <dt className="text-gray-500">Secret</dt>
          <dd className="flex items-center space-x-2">
            {secret ? (
              <>
                <code className="bg-gray-100 px-2 py-1 rounded">{secret}</code>
                <button onClick={() => setSecret(null)} className="text-gray-400 hover:text-gray-600" aria-label="Hide">
                  <EyeOff className="h-4 w-4" />
                </button>
              </>
            ) : (
              <button
                onClick={handleReveal}
                disabled={revealing}
                className="flex items-center text-blue-600 hover:text-blue-800 text-sm"
              >
                <Eye className="h-4 w-4 mr-1" />
                {revealing ? 'Revealing...' : 'Reveal Secret'}
              </button>
            )}
          </dd>
        </div>
        {entry.notes && (
          <div>
            <dt className="text-gray-500">Notes</dt>
            <dd className="text-gray-900 whitespace-pre-wrap">{entry.notes}</dd>
          </div>
        )}
        <div>
          <dt className="text-gray-500">Created by</dt>
          <dd className="text-gray-900">{entry.creatorName} on {new Date(entry.createdAt).toLocaleString()}</dd>
        </div>
      </dl>

      <button
        onClick={handleShowAudit}
        className="mt-4 flex items-center text-sm text-gray-500 hover:text-gray-700"
      >
        <Clock className="h-4 w-4 mr-1" />
        {showAudit ? 'Hide audit log' : 'Show audit log'}
      </button>

      {showAudit && auditLog && (
        <div className="mt-2 border-t pt-2 space-y-1 text-xs text-gray-600">
          {auditLog.length === 0 && <div className="italic text-gray-400">No audit events yet.</div>}
          {auditLog.map((entry) => (
            <div key={entry.id}>
              <span className="font-medium">{entry.action}</span> by {entry.userName} at{' '}
              {new Date(entry.timestamp).toLocaleString()}
              {entry.details && <span className="text-gray-400"> — {entry.details}</span>}
            </div>
          ))}
        </div>
      )}
    </div>
  );
};

export default VaultEntryDetail;
