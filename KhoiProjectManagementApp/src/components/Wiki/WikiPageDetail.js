// src/components/Wiki/WikiPageDetail.js
import React, { useState, useEffect } from 'react';
import ReactMarkdown from 'react-markdown';
import { Edit3, Trash2, Clock, Plus } from 'lucide-react';
import WikiPageEditor from './WikiPageEditor';
import WikiVersionHistory from './WikiVersionHistory';
import WikiComments from './WikiComments';
import ShareButton from '../Common/ShareButton';
import { hasSpaceLevel } from '../../utils/spaceLevel';

const WikiPageDetail = ({ apiService, pageId, myEffectiveLevel, currentUserId, onDeleted, onAddSubPage }) => {
  const [page, setPage] = useState(null);
  const [error, setError] = useState(null);
  const [editing, setEditing] = useState(false);
  const [showHistory, setShowHistory] = useState(false);

  const load = async () => {
    try {
      const result = await apiService.getWikiPage(pageId);
      setPage(result);
      setError(null);
    } catch (err) {
      setError(err.message);
    }
  };

  useEffect(() => {
    load();
    setEditing(false);
    setShowHistory(false);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [apiService, pageId]);

  const handleSave = async (data) => {
    await apiService.updateWikiPage(pageId, data);
    setEditing(false);
    await load();
  };

  const handleDelete = async () => {
    if (!window.confirm('Delete this page?')) return;
    await apiService.deleteWikiPage(pageId);
    onDeleted();
  };

  const canWrite = hasSpaceLevel(myEffectiveLevel, 'Write');
  const canManage = hasSpaceLevel(myEffectiveLevel, 'Manage');

  if (error) return <div className="text-red-600 p-4">{error}</div>;
  if (!page) return <div className="text-gray-400 p-4">Loading...</div>;

  if (editing) {
    return (
      <WikiPageEditor
        initialTitle={page.title}
        initialContent={page.contentMarkdown}
        isNew={false}
        draftKey={`khoi_draft_wiki_edit_${pageId}`}
        onSave={handleSave}
        onCancel={() => setEditing(false)}
      />
    );
  }

  return (
    <div className="bg-white rounded-lg shadow p-6">
      <div className="flex justify-between items-start mb-4">
        <h3 className="text-xl font-semibold text-gray-900">{page.title}</h3>
        <div className="flex space-x-2">
          <ShareButton
            url={`${window.location.origin}${window.location.pathname}?tab=wiki&spaceId=${page.spaceId}&pageId=${pageId}`}
            label={page.title}
          />
          {canWrite && (
            <button onClick={() => onAddSubPage(pageId)} className="text-gray-400 hover:text-gray-600" aria-label="Add sub-page">
              <Plus className="h-4 w-4" />
            </button>
          )}
          {canWrite && (
            <button onClick={() => setEditing(true)} className="text-gray-400 hover:text-gray-600" aria-label="Edit">
              <Edit3 className="h-4 w-4" />
            </button>
          )}
          {canManage && (
            <button onClick={handleDelete} className="text-red-400 hover:text-red-600" aria-label="Delete">
              <Trash2 className="h-4 w-4" />
            </button>
          )}
        </div>
      </div>

      <div className="text-xs text-gray-400 mb-4">
        v{page.currentVersionNumber} · created by {page.creatorName}
        {page.lastEditedByName && ` · last edited by ${page.lastEditedByName}`}
      </div>

      <div className="prose prose-sm max-w-none">
        <ReactMarkdown>{page.contentMarkdown}</ReactMarkdown>
      </div>

      <button
        onClick={() => setShowHistory(!showHistory)}
        className="mt-4 flex items-center text-sm text-gray-500 hover:text-gray-700"
      >
        <Clock className="h-4 w-4 mr-1" />
        {showHistory ? 'Hide version history' : 'Show version history'}
      </button>
      {showHistory && (
        <div className="mt-2">
          <WikiVersionHistory apiService={apiService} pageId={pageId} />
        </div>
      )}

      <div className="mt-6 border-t pt-4">
        <WikiComments apiService={apiService} pageId={pageId} currentUserId={currentUserId} myEffectiveLevel={myEffectiveLevel} />
      </div>
    </div>
  );
};

export default WikiPageDetail;
