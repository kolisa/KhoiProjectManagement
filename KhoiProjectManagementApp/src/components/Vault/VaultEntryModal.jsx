// src/components/Vault/VaultEntryModal.js
// Create/edit form for a VaultEntry. Visual pattern matches App.js's existing inline modals
// (e.g. "Add Project") for consistency, even though those live inline rather than in this file.
import React, { useState } from 'react';
import { validateVaultEntry, hasErrors } from '../../utils/validation';

const VaultEntryModal = ({ spaceId, entry, onSave, onClose }) => {
  const [form, setForm] = useState({
    name: entry?.name || '',
    systemOrUrl: entry?.systemOrUrl || '',
    username: entry?.username || '',
    secretValue: '',
    notes: entry?.notes || '',
  });
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState(null);
  const [fieldErrors, setFieldErrors] = useState({});

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (saving) return; // guards against a double-click firing two submits

    const validationErrors = validateVaultEntry(form, { isCreate: !entry });
    setFieldErrors(validationErrors);
    if (hasErrors(validationErrors)) return;

    setSaving(true);
    setError(null);
    try {
      if (entry) {
        await onSave({
          name: form.name,
          systemOrUrl: form.systemOrUrl,
          username: form.username,
          secretValue: form.secretValue || undefined,
          notes: form.notes,
        });
      } else {
        await onSave({
          name: form.name,
          spaceId,
          systemOrUrl: form.systemOrUrl,
          username: form.username,
          secretValue: form.secretValue,
          notes: form.notes,
        });
      }
    } catch (err) {
      setError(err.message);
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="fixed inset-0 bg-gray-600 bg-opacity-50 flex items-center justify-center p-4 z-50">
      <div className="bg-white rounded-lg max-w-md w-full p-6 max-h-screen overflow-y-auto">
        <h3 className="text-lg font-semibold mb-4">{entry ? 'Edit Vault Entry' : 'New Vault Entry'}</h3>
        {error && <div className="mb-3 text-sm text-red-600">{error}</div>}
        <form onSubmit={handleSubmit} className="space-y-4">
          <div>
            <input
              type="text"
              placeholder="Name"
              value={form.name}
              onChange={(e) => setForm({ ...form, name: e.target.value })}
              className={`w-full border rounded-lg px-3 py-2 ${fieldErrors.name ? 'border-red-400' : 'border-gray-300'}`}
              aria-invalid={!!fieldErrors.name}
              required
            />
            {fieldErrors.name && <p className="text-xs text-red-600 mt-1">{fieldErrors.name}</p>}
          </div>
          <input
            type="text"
            placeholder="System / URL"
            value={form.systemOrUrl}
            onChange={(e) => setForm({ ...form, systemOrUrl: e.target.value })}
            className="w-full border border-gray-300 rounded-lg px-3 py-2"
          />
          <input
            type="text"
            placeholder="Username"
            value={form.username}
            onChange={(e) => setForm({ ...form, username: e.target.value })}
            className="w-full border border-gray-300 rounded-lg px-3 py-2"
          />
          <div>
            <input
              type="password"
              placeholder={entry ? 'New secret (leave blank to keep current)' : 'Secret'}
              value={form.secretValue}
              onChange={(e) => setForm({ ...form, secretValue: e.target.value })}
              className={`w-full border rounded-lg px-3 py-2 ${fieldErrors.secretValue ? 'border-red-400' : 'border-gray-300'}`}
              aria-invalid={!!fieldErrors.secretValue}
              required={!entry}
            />
            {fieldErrors.secretValue && <p className="text-xs text-red-600 mt-1">{fieldErrors.secretValue}</p>}
          </div>
          <textarea
            placeholder="Notes"
            value={form.notes}
            onChange={(e) => setForm({ ...form, notes: e.target.value })}
            className="w-full border border-gray-300 rounded-lg px-3 py-2"
            rows="3"
          />
          <div className="flex space-x-3">
            <button
              type="submit"
              disabled={saving}
              className="flex-1 bg-blue-600 text-white py-2 rounded-lg hover:bg-blue-700 disabled:opacity-50"
            >
              {saving ? 'Saving...' : entry ? 'Save Changes' : 'Create Entry'}
            </button>
            <button
              type="button"
              onClick={onClose}
              className="flex-1 bg-gray-300 text-gray-700 py-2 rounded-lg hover:bg-gray-400"
            >
              Cancel
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};

export default VaultEntryModal;
