// src/components/Ideas/IdeasPage.js
import React, { useState, useEffect } from 'react';
import { Lightbulb, Plus, X, MessageSquare } from 'lucide-react';
import IdeaDetail from './IdeaDetail';
import { hasPermission } from '../../utils/permissions';
import { useToast } from '../../contexts/ToastContext';
import { validateIdea, hasErrors } from '../../utils/validation';
import useModalA11y from '../Common/useModalA11y';

const COLUMNS = [
  { key: 'Submitted', label: 'Submitted' },
  { key: 'UnderReview', label: 'Under Review' },
  { key: 'Approved', label: 'Approved' },
  { key: 'Rejected', label: 'Rejected' },
  { key: 'ConvertedToProject', label: 'Converted to Project' },
];

const formatIdeaAge = (iso) => {
  const days = Math.floor((Date.now() - new Date(iso).getTime()) / 86400000);
  if (days < 1) return 'today';
  if (days === 1) return '1d';
  return `${days}d`;
};

const NewIdeaModal = ({ onSave, onClose }) => {
  const modalRef = useModalA11y(onClose);
  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState(null);

  const handleSave = async () => {
    const validationErrors = validateIdea({ title, description });
    if (hasErrors(validationErrors)) {
      setError(Object.values(validationErrors)[0]);
      return;
    }

    setSaving(true);
    setError(null);
    try {
      await onSave({ title, description });
    } catch (err) {
      setError(err.message);
      setSaving(false);
    }
  };

  return (
    <div className="fixed inset-0 bg-gray-900/50 backdrop-blur-sm flex items-center justify-center p-4 z-50">
      <div ref={modalRef} role="dialog" aria-modal="true" aria-labelledby="new-idea-modal-title" tabIndex={-1} className="bg-white rounded-2xl shadow-xl overflow-hidden w-full max-w-lg outline-none">
        <div className="px-6 py-4 border-b border-gray-100 flex items-center justify-between">
          <h3 id="new-idea-modal-title" className="text-base font-semibold text-gray-900">New Idea</h3>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600" aria-label="Close">
            <X className="h-4 w-4" />
          </button>
        </div>
        <div className="px-6 py-5 space-y-4">
          {error && <div className="text-red-600 text-sm">{error}</div>}
          <input
            type="text"
            placeholder="Title"
            value={title}
            onChange={(e) => setTitle(e.target.value)}
            className="w-full border border-gray-300 rounded-[10px] px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow"
          />
          <textarea
            placeholder="Describe your idea..."
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            rows={5}
            className="w-full border border-gray-300 rounded-[10px] px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow"
          />
        </div>
        <div className="px-6 py-4 border-t border-gray-100 flex justify-end gap-3">
          <button onClick={onClose} className="inline-flex items-center gap-2 bg-white text-gray-700 border border-gray-300 px-4 py-2.5 rounded-[10px] text-sm font-semibold hover:bg-gray-50 transition-colors">Cancel</button>
          <button
            onClick={handleSave}
            disabled={saving || !title.trim() || !description.trim()}
            className="inline-flex items-center gap-2 bg-blue-600 text-white px-4 py-2.5 rounded-[10px] text-sm font-semibold hover:bg-blue-700 shadow-sm transition-colors disabled:opacity-50"
          >
            {saving ? 'Submitting...' : 'Submit Idea'}
          </button>
        </div>
      </div>
    </div>
  );
};

const IdeasPage = ({ apiService, user }) => {
  const toast = useToast();
  const [ideas, setIdeas] = useState(null);
  const [error, setError] = useState(null);
  const [selectedId, setSelectedId] = useState(null);
  const [showNewIdea, setShowNewIdea] = useState(false);
  const ideaDetailModalRef = useModalA11y(() => setSelectedId(null));

  const load = async () => {
    try {
      const result = await apiService.getIdeas();
      setIdeas(result || []);
    } catch (err) {
      setError(err.message);
      // Without this, `ideas` stays null forever on a failed load, so the board keeps showing its
      // "Loading..." placeholder underneath the error message instead of settling into an empty state.
      setIdeas([]);
    }
  };

  useEffect(() => {
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const handleCreate = async (dto) => {
    // Not try/caught - NewIdeaModal's own onSave await/catch needs the rejection for its inline
    // error and to keep the modal open with what the user typed.
    const created = await apiService.createIdea(dto);
    setShowNewIdea(false);
    await load();
    setSelectedId(created.id);
    toast.success('Idea submitted.');
  };

  return (
    <div className="space-y-6">
      <div className="flex justify-between items-center">
        <div>
          <h2 className="text-[27px] font-bold text-gray-900 flex items-center">
            <Lightbulb className="h-7 w-7 mr-2 text-gray-700" />
            Ideas Board
          </h2>
          <p className="text-gray-600">
            {ideas ? `${ideas.length} idea${ideas.length !== 1 ? 's' : ''}` : 'Share ideas, attach mockups, and turn the best ones into projects'}
            {ideas && hasPermission(user?.permissions, 'ideas.manage') && (() => {
              const waiting = ideas.filter((i) => i.status === 'Submitted' || i.status === 'UnderReview').length;
              return waiting > 0 ? ` · ${waiting} waiting on your review` : '';
            })()}
          </p>
        </div>
        <button
          onClick={() => setShowNewIdea(true)}
          className="inline-flex items-center gap-2 bg-blue-600 text-white px-4 py-2.5 rounded-[10px] text-sm font-semibold hover:bg-blue-700 shadow-sm transition-colors"
        >
          <Plus className="h-5 w-5" />
          New Idea
        </button>
      </div>

      {error && <div className="text-red-600 text-sm">{error}</div>}

      {ideas === null ? (
        <div className="bg-white rounded-2xl border border-gray-100 shadow-sm p-6 text-gray-400">Loading...</div>
      ) : (
        <div className="flex gap-4 overflow-x-auto pb-2">
          {COLUMNS.map((col) => {
            const columnIdeas = ideas.filter((i) => i.status === col.key);
            return (
              <div key={col.key} className="w-72 flex-shrink-0 flex flex-col">
                <div className="flex items-center gap-2 px-1 pb-2">
                  <h3 className="text-sm font-semibold text-gray-700">{col.label}</h3>
                  <span className="text-xs font-medium text-gray-400 bg-gray-100 rounded-full px-2 py-0.5">{columnIdeas.length}</span>
                </div>
                <div className="bg-gray-50 rounded-2xl border border-gray-100 flex-1 min-h-[120px] p-2 space-y-2">
                  {columnIdeas.length === 0 && (
                    <div className="text-center text-xs text-gray-400 py-6">No ideas here</div>
                  )}
                  {columnIdeas.map((idea) => (
                    <div
                      key={idea.id}
                      onClick={() => setSelectedId(idea.id)}
                      className="bg-white rounded-2xl border border-gray-100 shadow-sm p-3 cursor-pointer hover:shadow-md transition-shadow"
                    >
                      <p className="text-sm font-medium text-gray-900 line-clamp-2">{idea.title}</p>
                      <div className="flex items-center justify-between mt-2 text-xs text-gray-500">
                        <span>{idea.submitterName}</span>
                        <div className="flex items-center gap-2">
                          {idea.commentCount > 0 && (
                            <span className="flex items-center gap-0.5">
                              <MessageSquare className="h-3 w-3" />
                              {idea.commentCount}
                            </span>
                          )}
                          <span>{formatIdeaAge(idea.createdAt)}</span>
                        </div>
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            );
          })}
        </div>
      )}

      {selectedId && (
        <div
          className="fixed inset-0 bg-gray-900/50 backdrop-blur-sm flex items-center justify-center p-4 z-50"
          onClick={(e) => { if (e.target === e.currentTarget) setSelectedId(null); }}
        >
          <div ref={ideaDetailModalRef} role="dialog" aria-modal="true" aria-label="Idea details" tabIndex={-1} className="w-full max-w-2xl max-h-[90vh] overflow-y-auto outline-none">
            <IdeaDetail
              apiService={apiService}
              user={user}
              ideaId={selectedId}
              onClose={() => setSelectedId(null)}
              onChanged={load}
            />
          </div>
        </div>
      )}

      {showNewIdea && (
        <NewIdeaModal onSave={handleCreate} onClose={() => setShowNewIdea(false)} />
      )}
    </div>
  );
};

export default IdeasPage;
