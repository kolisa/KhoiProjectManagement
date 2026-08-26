// src/components/Wiki/WikiPageEditor.js
// A raw Markdown textarea (title + content + optional edit summary) - no WYSIWYG, matching the
// Phase 4 decision to keep wiki content as plain Markdown text.
import React, { useState, useEffect, useRef } from 'react';
import { Save } from 'lucide-react';
import { validateWikiPage, hasErrors } from '../../utils/validation';

const AUTOSAVE_DEBOUNCE_MS = 1500;

const WikiPageEditor = ({ initialTitle, initialContent, isNew, draftKey, onSave, onCancel }) => {
  const [title, setTitle] = useState(initialTitle || '');
  const [content, setContent] = useState(initialContent || '');
  const [editSummary, setEditSummary] = useState('');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState(null);
  const [draftRestoredAt, setDraftRestoredAt] = useState(null);
  const [pendingDraft, setPendingDraft] = useState(null);
  const debounceRef = useRef(null);

  // On mount, offer to restore a draft left behind by an interrupted session (crash, auto-logout,
  // accidental navigation) rather than silently overwriting it or silently discarding it.
  useEffect(() => {
    if (!draftKey) return;
    try {
      const raw = localStorage.getItem(draftKey);
      if (!raw) return;
      const draft = JSON.parse(raw);
      if (draft && (draft.title !== (initialTitle || '') || draft.content !== (initialContent || ''))) {
        setPendingDraft(draft);
      }
    } catch {
      // Corrupt draft entry - ignore rather than block editing.
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [draftKey]);

  // Debounced autosave to localStorage - never blocks typing, never touches the server. This is a
  // last-resort local safety net (crash, accidental tab close, auto-logout), not a substitute for
  // actually saving the page.
  useEffect(() => {
    if (!draftKey) return;
    if (debounceRef.current) clearTimeout(debounceRef.current);
    debounceRef.current = setTimeout(() => {
      try {
        localStorage.setItem(draftKey, JSON.stringify({ title, content, editSummary, savedAt: Date.now() }));
        setDraftRestoredAt(Date.now());
      } catch {
        // localStorage full/unavailable - autosave is best-effort, never fatal to editing.
      }
    }, AUTOSAVE_DEBOUNCE_MS);
    return () => clearTimeout(debounceRef.current);
  }, [draftKey, title, content, editSummary]);

  const clearDraft = () => {
    if (draftKey) localStorage.removeItem(draftKey);
  };

  const handleRestoreDraft = () => {
    setTitle(pendingDraft.title);
    setContent(pendingDraft.content);
    setEditSummary(pendingDraft.editSummary || '');
    setPendingDraft(null);
  };

  const handleDiscardDraft = () => {
    clearDraft();
    setPendingDraft(null);
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (saving) return; // guards against a double-click firing two submits

    // Title's empty check is already enforced by the input's native `required` attribute - this adds
    // the max-length rule the backend validator also carries, which `required` alone doesn't cover.
    const validationErrors = validateWikiPage({ title });
    if (hasErrors(validationErrors)) {
      setError(Object.values(validationErrors)[0]);
      return;
    }

    setSaving(true);
    setError(null);
    try {
      await onSave({ title, contentMarkdown: content, editSummary: editSummary || undefined });
      clearDraft();
    } catch (err) {
      setError(err.message);
    } finally {
      setSaving(false);
    }
  };

  const handleCancel = () => {
    clearDraft();
    onCancel();
  };

  return (
    <form onSubmit={handleSubmit} className="bg-white rounded-2xl border border-gray-100 shadow-sm p-6 space-y-4">
      {pendingDraft && (
        <div className="bg-amber-50 border border-amber-200 rounded-lg p-3 flex items-center justify-between text-sm">
          <span className="text-amber-800">
            An unsaved draft from {new Date(pendingDraft.savedAt).toLocaleString()} was found.
          </span>
          <div className="space-x-2 flex-shrink-0 ml-3">
            <button type="button" onClick={handleRestoreDraft} className="text-blue-600 hover:text-blue-800 font-medium transition-colors">
              Restore
            </button>
            <button type="button" onClick={handleDiscardDraft} className="text-gray-500 hover:text-gray-700 transition-colors">
              Discard
            </button>
          </div>
        </div>
      )}
      <input
        type="text"
        placeholder="Page title"
        value={title}
        onChange={(e) => setTitle(e.target.value)}
        className="w-full border border-gray-300 rounded-[10px] px-3.5 py-2.5 text-lg font-medium focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow"
        required
      />
      <textarea
        placeholder="Write in Markdown..."
        value={content}
        onChange={(e) => setContent(e.target.value)}
        className="w-full border border-gray-300 rounded-[10px] px-3.5 py-2.5 font-mono text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow"
        rows="16"
      />
      {!isNew && (
        <input
          type="text"
          placeholder="What changed? (optional)"
          value={editSummary}
          onChange={(e) => setEditSummary(e.target.value)}
          className="w-full border border-gray-300 rounded-[10px] px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow"
        />
      )}
      {error && <div className="text-sm text-red-600">{error}</div>}
      <div className="flex items-center justify-between">
        <div className="flex space-x-3">
          <button
            type="submit"
            disabled={saving}
            className="inline-flex items-center gap-2 bg-blue-600 text-white px-4 py-2.5 rounded-[10px] text-sm font-semibold hover:bg-blue-700 shadow-sm transition-colors disabled:opacity-50"
          >
            {saving ? 'Saving...' : isNew ? 'Create Page' : 'Save Changes'}
          </button>
          <button
            type="button"
            onClick={handleCancel}
            className="inline-flex items-center gap-2 bg-white text-gray-700 border border-gray-300 px-4 py-2.5 rounded-[10px] text-sm font-semibold hover:bg-gray-50 transition-colors"
          >
            Cancel
          </button>
        </div>
        {draftKey && draftRestoredAt && (
          <span className="text-xs text-gray-400 flex items-center">
            <Save className="h-3 w-3 mr-1" />
            Draft saved locally
          </span>
        )}
      </div>
    </form>
  );
};

export default WikiPageEditor;
