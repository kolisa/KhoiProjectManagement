// src/components/Vault/VaultEntryModal.js
// Create/edit form for a VaultEntry. Visual pattern matches App.js's existing inline modals
// (e.g. "Add Project") for consistency, even though those live inline rather than in this file.
import React, { useState } from 'react';
import { X } from 'lucide-react';
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
    <div className="fixed inset-0 bg-gray-900/50 backdrop-blur-sm flex items-center justify-center p-4 z-50">
      <div className="bg-white rounded-2xl shadow-xl w-full max-w-md overflow-hidden max-h-screen flex flex-col">
        <div className="px-6 py-4 border-b border-gray-100 flex items-center justify-between">
          <h3 className="text-base font-semibold text-gray-900">{entry ? 'Edit Vault Entry' : 'New Vault Entry'}</h3>
          <button
            type="button"
            onClick={onClose}
            className="text-gray-400 hover:text-gray-600"
            aria-label="Close"
          >
            <X className="h-5 w-5" />
          </button>
        </div>
        <form onSubmit={handleSubmit} className="flex flex-col overflow-y-auto">
          <div className="px-6 py-5 space-y-4">
            {error && <div className="text-sm text-red-600">{error}</div>}
            <div>
              <input
                type="text"
                placeholder="Name"
                value={form.name}
                onChange={(e) => setForm({ ...form, name: e.target.value })}
                className={`w-full border rounded-lg px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow ${fieldErrors.name ? 'border-red-400' : 'border-gray-300'}`}
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
              className="w-full border border-gray-300 rounded-[10px] px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow"
            />
            <input
              type="text"
              placeholder="Username"
              value={form.username}
              onChange={(e) => setForm({ ...form, username: e.target.value })}
              className="w-full border border-gray-300 rounded-[10px] px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow"
            />
            <div>
              <input
                type="password"
                placeholder={entry ? 'New secret (leave blank to keep current)' : 'Secret'}
                value={form.secretValue}
                onChange={(e) => setForm({ ...form, secretValue: e.target.value })}
                className={`w-full border rounded-lg px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow ${fieldErrors.secretValue ? 'border-red-400' : 'border-gray-300'}`}
                aria-invalid={!!fieldErrors.secretValue}
                required={!entry}
              />
              {fieldErrors.secretValue && <p className="text-xs text-red-600 mt-1">{fieldErrors.secretValue}</p>}
            </div>
            <textarea
              placeholder="Notes"
              value={form.notes}
              onChange={(e) => setForm({ ...form, notes: e.target.value })}
              className="w-full border border-gray-300 rounded-[10px] px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow"
              rows="3"
            />
          </div>
          <div className="px-6 py-4 border-t border-gray-100 flex justify-end gap-3">
            <button
              type="submit"
              disabled={saving}
              className="inline-flex items-center gap-2 bg-blue-600 text-white px-4 py-2.5 rounded-[10px] text-sm font-semibold hover:bg-blue-700 shadow-sm transition-colors disabled:opacity-50"
            >
              {saving ? 'Saving...' : entry ? 'Save Changes' : 'Create Entry'}
            </button>
            <button
              type="button"
              onClick={onClose}
              className="inline-flex items-center gap-2 bg-white text-gray-700 border border-gray-300 px-4 py-2.5 rounded-[10px] text-sm font-semibold hover:bg-gray-50 transition-colors"
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
