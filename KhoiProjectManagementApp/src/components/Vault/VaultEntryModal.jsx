// src/components/Vault/VaultEntryModal.js
// Create/edit form for a VaultEntry. Visual pattern matches App.js's existing inline modals
// (e.g. "Add Project") for consistency, even though those live inline rather than in this file.
import React, { useState } from 'react';
import { X } from 'lucide-react';
import { validateVaultEntry, hasErrors } from '../../utils/validation';
import { Button, Input, Textarea, Modal } from '../ui';

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
    <Modal
      onClose={onClose}
      closeOnBackdrop={false}
      titleId="vault-entry-modal-title"
      size="md"
      padded={false}
      scroll={false}
      className="flex flex-col overflow-hidden max-h-screen"
      header={(
        <div className="px-6 py-4 border-b border-gray-100 flex items-center justify-between">
          <h3 id="vault-entry-modal-title" className="text-base font-semibold text-gray-900">{entry ? 'Edit Vault Entry' : 'New Vault Entry'}</h3>
          <button
            type="button"
            onClick={onClose}
            className="text-gray-400 hover:text-gray-600"
            aria-label="Close"
          >
            <X className="h-5 w-5" />
          </button>
        </div>
      )}
    >
        <form onSubmit={handleSubmit} className="flex flex-col overflow-y-auto">
          <div className="px-6 py-5 space-y-4">
            {error && <div className="text-sm text-red-600">{error}</div>}
            <div>
              <Input
                type="text"
                placeholder="Name"
                value={form.name}
                onChange={(e) => setForm({ ...form, name: e.target.value })}
                invalid={!!fieldErrors.name}
                required
              />
              {fieldErrors.name && <p className="text-xs text-red-600 mt-1">{fieldErrors.name}</p>}
            </div>
            <Input
              type="text"
              placeholder="System / URL"
              value={form.systemOrUrl}
              onChange={(e) => setForm({ ...form, systemOrUrl: e.target.value })}
            />
            <Input
              type="text"
              placeholder="Username"
              value={form.username}
              onChange={(e) => setForm({ ...form, username: e.target.value })}
            />
            <div>
              <Input
                type="password"
                placeholder={entry ? 'New secret (leave blank to keep current)' : 'Secret'}
                value={form.secretValue}
                onChange={(e) => setForm({ ...form, secretValue: e.target.value })}
                invalid={!!fieldErrors.secretValue}
                required={!entry}
              />
              {fieldErrors.secretValue && <p className="text-xs text-red-600 mt-1">{fieldErrors.secretValue}</p>}
            </div>
            <Textarea
              placeholder="Notes"
              value={form.notes}
              onChange={(e) => setForm({ ...form, notes: e.target.value })}
              rows="3"
            />
          </div>
          <div className="px-6 py-4 border-t border-gray-100 flex justify-end gap-3">
            <Button type="submit" disabled={saving}>
              {saving ? 'Saving...' : entry ? 'Save Changes' : 'Create Entry'}
            </Button>
            <Button type="button" variant="outline" onClick={onClose}>
              Cancel
            </Button>
          </div>
        </form>
    </Modal>
  );
};

export default VaultEntryModal;
