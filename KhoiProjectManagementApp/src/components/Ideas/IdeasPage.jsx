// src/components/Ideas/IdeasPage.js
import React, { useState, useEffect } from 'react';
import { Lightbulb, Plus, X, MessageSquare } from 'lucide-react';
import IdeaDetail from './IdeaDetail';

const STATUS_COLORS = {
  Submitted: 'bg-gray-50 text-gray-700',
  UnderReview: 'bg-yellow-50 text-yellow-700',
  Approved: 'bg-green-50 text-green-700',
  Rejected: 'bg-red-50 text-red-700',
  ConvertedToProject: 'bg-blue-50 text-blue-700',
};

const STATUS_DOT_COLORS = {
  Submitted: 'bg-gray-500',
  UnderReview: 'bg-yellow-500',
  Approved: 'bg-green-500',
  Rejected: 'bg-red-500',
  ConvertedToProject: 'bg-blue-500',
};

const NewIdeaModal = ({ onSave, onClose }) => {
  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState(null);

  const handleSave = async () => {
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
      <div className="bg-white rounded-2xl shadow-xl overflow-hidden w-full max-w-lg">
        <div className="px-6 py-4 border-b border-gray-100 flex items-center justify-between">
          <h3 className="text-base font-semibold text-gray-900">New Idea</h3>
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
            className="w-full border border-gray-300 rounded-lg px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow"
          />
          <textarea
            placeholder="Describe your idea..."
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            rows={5}
            className="w-full border border-gray-300 rounded-lg px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow"
          />
        </div>
        <div className="px-6 py-4 border-t border-gray-100 flex justify-end gap-3">
          <button onClick={onClose} className="inline-flex items-center gap-2 bg-white text-gray-700 border border-gray-300 px-4 py-2.5 rounded-lg text-sm font-semibold hover:bg-gray-50 transition-colors">Cancel</button>
          <button
            onClick={handleSave}
            disabled={saving || !title.trim() || !description.trim()}
            className="inline-flex items-center gap-2 bg-blue-600 text-white px-4 py-2.5 rounded-lg text-sm font-semibold hover:bg-blue-700 shadow-sm transition-colors disabled:opacity-50"
          >
            {saving ? 'Submitting...' : 'Submit Idea'}
          </button>
        </div>
      </div>
    </div>
  );
};

const IdeasPage = ({ apiService, user }) => {
  const [ideas, setIdeas] = useState(null);
  const [error, setError] = useState(null);
  const [statusFilter, setStatusFilter] = useState('');
  const [selectedId, setSelectedId] = useState(null);
  const [showNewIdea, setShowNewIdea] = useState(false);

  const load = async () => {
    try {
      const result = await apiService.getIdeas(statusFilter || undefined);
      setIdeas(result || []);
    } catch (err) {
      setError(err.message);
    }
  };

  useEffect(() => {
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [statusFilter]);

  const handleCreate = async (dto) => {
    const created = await apiService.createIdea(dto);
    setShowNewIdea(false);
    await load();
    setSelectedId(created.id);
  };

  return (
    <div className="space-y-6">
      <div className="flex justify-between items-center">
        <div>
          <h2 className="text-3xl font-bold text-gray-900 flex items-center">
            <Lightbulb className="h-7 w-7 mr-2 text-gray-700" />
            Ideas Board
          </h2>
          <p className="text-gray-600">Share ideas, attach mockups, and turn the best ones into projects</p>
        </div>
        <button
          onClick={() => setShowNewIdea(true)}
          className="inline-flex items-center gap-2 bg-blue-600 text-white px-4 py-2.5 rounded-lg text-sm font-semibold hover:bg-blue-700 shadow-sm transition-colors"
        >
          <Plus className="h-5 w-5" />
          New Idea
        </button>
      </div>

      <div className="flex space-x-2">
        {['', 'Submitted', 'UnderReview', 'Approved', 'Rejected', 'ConvertedToProject'].map((s) => (
          <button
            key={s || 'all'}
            onClick={() => setStatusFilter(s)}
            className={`text-sm px-3 py-1.5 rounded-lg font-medium transition-colors ${statusFilter === s ? 'bg-blue-600 text-white shadow-sm' : 'text-gray-600 hover:bg-gray-100'}`}
          >
            {s || 'All'}
          </button>
        ))}
      </div>

      {error && <div className="text-red-600 text-sm">{error}</div>}

      <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
        <div className="bg-white rounded-xl border border-gray-100 shadow-sm divide-y">
          {ideas === null && <div className="p-6 text-gray-400">Loading...</div>}
          {ideas?.length === 0 && <div className="p-6 text-center text-gray-400">No ideas yet.</div>}
          {ideas?.map((idea) => (
            <div
              key={idea.id}
              onClick={() => setSelectedId(idea.id)}
              className={`p-4 cursor-pointer hover:bg-gray-50/60 transition-colors ${selectedId === idea.id ? 'bg-blue-50/80' : ''}`}
            >
              <div className="flex justify-between items-start">
                <div className="font-medium text-gray-900">{idea.title}</div>
                <span className={`inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-semibold ${STATUS_COLORS[idea.status] || STATUS_COLORS.Submitted}`}>
                  <span className={`w-1.5 h-1.5 rounded-full ${STATUS_DOT_COLORS[idea.status] || STATUS_DOT_COLORS.Submitted}`} />
                  {idea.status}
                </span>
              </div>
              <div className="text-sm text-gray-500 mt-1 flex items-center">
                <span>{idea.submitterName}</span>
                <span className="mx-1">&middot;</span>
                <MessageSquare className="h-3.5 w-3.5 mr-1" />
                <span>{idea.commentCount}</span>
              </div>
            </div>
          ))}
        </div>

        <div>
          {selectedId ? (
            <IdeaDetail
              apiService={apiService}
              user={user}
              ideaId={selectedId}
              onClose={() => setSelectedId(null)}
              onChanged={load}
            />
          ) : (
            <div className="bg-white rounded-xl border border-gray-100 shadow-sm p-8 text-center text-gray-400">
              Select an idea to view details.
            </div>
          )}
        </div>
      </div>

      {showNewIdea && (
        <NewIdeaModal onSave={handleCreate} onClose={() => setShowNewIdea(false)} />
      )}
    </div>
  );
};

export default IdeasPage;
