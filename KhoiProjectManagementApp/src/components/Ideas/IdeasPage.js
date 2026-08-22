// src/components/Ideas/IdeasPage.js
import React, { useState, useEffect } from 'react';
import { Lightbulb, Plus, X, MessageSquare } from 'lucide-react';
import IdeaDetail from './IdeaDetail';

const STATUS_COLORS = {
  Submitted: 'bg-gray-100 text-gray-800',
  UnderReview: 'bg-yellow-100 text-yellow-800',
  Approved: 'bg-green-100 text-green-800',
  Rejected: 'bg-red-100 text-red-800',
  ConvertedToProject: 'bg-blue-100 text-blue-800',
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
    <div className="fixed inset-0 bg-black bg-opacity-40 flex items-center justify-center z-50">
      <div className="bg-white rounded-lg shadow-lg p-6 w-full max-w-lg">
        <div className="flex justify-between items-center mb-4">
          <h3 className="text-lg font-semibold text-gray-900">New Idea</h3>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600" aria-label="Close">
            <X className="h-4 w-4" />
          </button>
        </div>
        {error && <div className="text-red-600 text-sm mb-3">{error}</div>}
        <input
          type="text"
          placeholder="Title"
          value={title}
          onChange={(e) => setTitle(e.target.value)}
          className="w-full border rounded-lg px-3 py-2 mb-3"
        />
        <textarea
          placeholder="Describe your idea..."
          value={description}
          onChange={(e) => setDescription(e.target.value)}
          rows={5}
          className="w-full border rounded-lg px-3 py-2 mb-4"
        />
        <div className="flex justify-end space-x-2">
          <button onClick={onClose} className="px-4 py-2 rounded-lg text-gray-600 hover:bg-gray-100">Cancel</button>
          <button
            onClick={handleSave}
            disabled={saving || !title.trim() || !description.trim()}
            className="bg-blue-600 text-white px-4 py-2 rounded-lg hover:bg-blue-700 disabled:opacity-50"
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
          className="bg-blue-600 text-white px-4 py-2 rounded-lg hover:bg-blue-700 flex items-center"
        >
          <Plus className="h-5 w-5 mr-2" />
          New Idea
        </button>
      </div>

      <div className="flex space-x-2">
        {['', 'Submitted', 'UnderReview', 'Approved', 'Rejected', 'ConvertedToProject'].map((s) => (
          <button
            key={s || 'all'}
            onClick={() => setStatusFilter(s)}
            className={`text-sm px-3 py-1.5 rounded-lg ${statusFilter === s ? 'bg-blue-600 text-white' : 'bg-gray-100 text-gray-600 hover:bg-gray-200'}`}
          >
            {s || 'All'}
          </button>
        ))}
      </div>

      {error && <div className="text-red-600 text-sm">{error}</div>}

      <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
        <div className="bg-white rounded-lg shadow divide-y">
          {ideas === null && <div className="p-6 text-gray-400">Loading...</div>}
          {ideas?.length === 0 && <div className="p-6 text-center text-gray-400">No ideas yet.</div>}
          {ideas?.map((idea) => (
            <div
              key={idea.id}
              onClick={() => setSelectedId(idea.id)}
              className={`p-4 cursor-pointer hover:bg-gray-50 ${selectedId === idea.id ? 'bg-blue-50' : ''}`}
            >
              <div className="flex justify-between items-start">
                <div className="font-medium text-gray-900">{idea.title}</div>
                <span className={`text-xs px-2 py-0.5 rounded-full ${STATUS_COLORS[idea.status] || STATUS_COLORS.Submitted}`}>
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
            <div className="bg-white rounded-lg shadow p-8 text-center text-gray-400">
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
