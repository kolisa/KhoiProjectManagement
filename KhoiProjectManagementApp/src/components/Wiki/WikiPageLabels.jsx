// src/components/Wiki/WikiPageLabels.js
// Reuses the shared Tag concept already used for Project/Task tagging - full-replace on every
// add/remove (matches SetWikiPageLabelsDto's shape on the backend).
import React, { useState } from 'react';
import { Tag as TagIcon, X, Plus } from 'lucide-react';

const WikiPageLabels = ({ apiService, pageId, labels, canWrite, onChanged }) => {
  const [adding, setAdding] = useState(false);
  const [newLabel, setNewLabel] = useState('');
  const [saving, setSaving] = useState(false);

  const persist = async (nextLabels) => {
    setSaving(true);
    try {
      await apiService.setWikiPageLabels(pageId, nextLabels);
      await onChanged();
    } finally {
      setSaving(false);
    }
  };

  const handleAdd = async () => {
    const label = newLabel.trim().toLowerCase();
    if (!label || labels.includes(label)) {
      setNewLabel('');
      setAdding(false);
      return;
    }
    await persist([...labels, label]);
    setNewLabel('');
    setAdding(false);
  };

  const handleRemove = async (label) => {
    await persist(labels.filter((l) => l !== label));
  };

  return (
    <div className="flex items-center flex-wrap gap-1.5 mb-3">
      <TagIcon className="h-3.5 w-3.5 text-gray-400" />
      {labels.map((l) => (
        <span key={l} className="inline-flex items-center text-xs bg-gray-100 text-gray-700 pl-2 pr-1 py-0.5 rounded-full">
          {l}
          {canWrite && (
            <button onClick={() => handleRemove(l)} disabled={saving} className="ml-1 text-gray-400 hover:text-red-600" aria-label={`Remove label ${l}`}>
              <X className="h-3 w-3" />
            </button>
          )}
        </span>
      ))}
      {canWrite && !adding && (
        <button onClick={() => setAdding(true)} className="text-xs text-gray-400 hover:text-blue-600 flex items-center">
          <Plus className="h-3 w-3 mr-0.5" />
          Add label
        </button>
      )}
      {canWrite && adding && (
        <input
          type="text"
          autoFocus
          value={newLabel}
          onChange={(e) => setNewLabel(e.target.value)}
          onKeyDown={(e) => { if (e.key === 'Enter') handleAdd(); if (e.key === 'Escape') { setAdding(false); setNewLabel(''); } }}
          onBlur={handleAdd}
          placeholder="label name"
          className="text-xs border rounded px-1.5 py-0.5 w-24"
        />
      )}
    </div>
  );
};

export default WikiPageLabels;
