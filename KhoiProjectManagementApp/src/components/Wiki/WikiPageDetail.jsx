// src/components/Wiki/WikiPageDetail.js
import React, { useState, useEffect, useRef } from 'react';
import { Edit3, Trash2, Clock, Plus, Lock } from 'lucide-react';
import WikiPageEditor from './WikiPageEditor';
import WikiVersionHistory from './WikiVersionHistory';
import WikiComments from './WikiComments';
import WikiContentBlocks from './WikiContentBlocks';
import WikiPageLabels from './WikiPageLabels';
import WikiPresence from './WikiPresence';
import ShareButton from '../Common/ShareButton';
import { hasSpaceLevel } from '../../utils/spaceLevel';
import { createWikiHubConnection, HubConnectionState } from '../../services/wikiHub';
import { useToast } from '../../contexts/ToastContext';
import { useConfirm } from '../../contexts/ConfirmContext';
import { reportApiError } from '../../utils/apiError';

const WikiPageDetail = ({ apiService, pageId, myEffectiveLevel, currentUserId, onDeleted, onAddSubPage }) => {
  const toast = useToast();
  const confirm = useConfirm();
  const [page, setPage] = useState(null);
  const [comments, setComments] = useState([]);
  const [error, setError] = useState(null);
  const [editing, setEditing] = useState(false);
  const [showHistory, setShowHistory] = useState(false);
  const [viewers, setViewers] = useState([]);
  const [editLock, setEditLock] = useState(null);
  const hubRef = useRef(null);

  // Presence + edit lock: one connection per page view, started on mount and stopped on unmount.
  // Stopping the connection is itself what releases this viewer's presence/lock server-side
  // (WikiHub.OnDisconnectedAsync) - there's no separate "leave" call to remember here. If the hub is
  // unreachable, presence/locking silently does nothing; viewing and editing still work via the normal
  // REST endpoints, which enforce their own permission checks regardless of the lock's state.
  useEffect(() => {
    const connection = createWikiHubConnection(apiService);
    hubRef.current = connection;

    connection.on('PresenceUpdated', (updatedPageId, updatedViewers) => {
      if (updatedPageId === pageId) setViewers(updatedViewers || []);
    });
    connection.on('EditLockChanged', (updatedPageId, lock) => {
      if (updatedPageId === pageId) setEditLock(lock || null);
    });

    // A reconnect gets a new connectionId server-side, so presence/lock tracking for the old one is
    // already gone by the time this fires - rejoin to re-register under the new connection.
    connection.onreconnected(() => {
      connection.invoke('JoinPage', pageId).catch(() => {});
    });

    connection.start()
      .then(() => connection.invoke('JoinPage', pageId))
      .catch(() => {});

    return () => {
      hubRef.current = null;
      connection.stop().catch(() => {});
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [apiService, pageId]);

  const load = async () => {
    try {
      const [pageResult, commentsResult] = await Promise.all([
        apiService.getWikiPage(pageId),
        apiService.getWikiComments(pageId),
      ]);
      setPage(pageResult);
      setComments(commentsResult || []);
      setError(null);
    } catch (err) {
      setError(err.message);
    }
  };

  const reloadComments = async () => {
    const result = await apiService.getWikiComments(pageId);
    setComments(result || []);
  };

  useEffect(() => {
    load();
    setEditing(false);
    setShowHistory(false);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [apiService, pageId]);

  const handleAddAnchoredComment = async (body, anchorBlockIndex, anchorText) => {
    try {
      await apiService.addWikiComment(pageId, { body, anchorBlockIndex, anchorText });
      await reloadComments();
    } catch (err) {
      reportApiError(toast, err, 'Could not post comment.');
    }
  };

  const handleDeleteAnchoredComment = async (commentId) => {
    if (!(await confirm('Delete this comment?', { title: 'Delete comment', confirmText: 'Delete', danger: true }))) return;
    try {
      await apiService.deleteWikiComment(commentId);
      await reloadComments();
    } catch (err) {
      reportApiError(toast, err, 'Could not delete comment.');
    }
  };

  const releaseEditLock = () => {
    const conn = hubRef.current;
    if (conn && conn.state === HubConnectionState.Connected) {
      conn.invoke('StopEditing', pageId).catch(() => {});
    }
  };

  const handleEditClick = async () => {
    const conn = hubRef.current;
    if (conn && conn.state === HubConnectionState.Connected) {
      try {
        const granted = await conn.invoke('StartEditing', pageId);
        if (!granted) return; // someone else holds the lock - editLock state already reflects who
      } catch (err) {
        // hub denied/unreachable - fall back to allowing the attempt; PUT still enforces Write server-side
      }
    }
    setEditing(true);
  };

  const handleSave = async (data) => {
    // Deliberately NOT caught here - WikiPageEditor's own onSave await/catch needs the rejection to
    // reach it, so it can show an inline error next to Save AND keep the user's edits + localStorage
    // draft intact (it only clears the draft after onSave resolves without throwing).
    await apiService.updateWikiPage(pageId, data);
    releaseEditLock();
    setEditing(false);
    await load();
    toast.success('Page saved.');
  };

  const handleCancelEdit = () => {
    releaseEditLock();
    setEditing(false);
  };

  const handleDelete = async () => {
    if (!(await confirm('Delete this page?', { title: 'Delete page', confirmText: 'Delete', danger: true }))) return;
    try {
      await apiService.deleteWikiPage(pageId);
      toast.success('Page deleted.');
      onDeleted();
    } catch (err) {
      reportApiError(toast, err, 'Could not delete this page.');
    }
  };

  const canWrite = hasSpaceLevel(myEffectiveLevel, 'Write');
  const canManage = hasSpaceLevel(myEffectiveLevel, 'Manage');
  const lockedByOther = editLock && editLock.userId !== currentUserId;

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
        onCancel={handleCancelEdit}
      />
    );
  }

  return (
    <div className="bg-white rounded-2xl border border-gray-100 shadow-sm p-6">
      <div className="flex justify-between items-start mb-4">
        <h3 className="text-xl font-semibold text-gray-900">{page.title}</h3>
        <div className="flex space-x-2">
          <ShareButton
            url={`${window.location.origin}${window.location.pathname}?tab=wiki&spaceId=${page.spaceId}&pageId=${pageId}`}
            label={page.title}
          />
          {canWrite && (
            <button onClick={() => onAddSubPage(pageId)} className="text-gray-400 hover:text-gray-600 transition-colors" aria-label="Add sub-page">
              <Plus className="h-4 w-4" />
            </button>
          )}
          {canWrite && (
            <button
              onClick={handleEditClick}
              disabled={lockedByOther}
              className={lockedByOther ? 'text-gray-200 cursor-not-allowed' : 'text-gray-400 hover:text-gray-600 transition-colors'}
              aria-label="Edit"
              title={lockedByOther ? `${editLock.userName} is editing this page` : 'Edit'}
            >
              {lockedByOther ? <Lock className="h-4 w-4" /> : <Edit3 className="h-4 w-4" />}
            </button>
          )}
          {canManage && (
            <button onClick={handleDelete} className="text-red-400 hover:text-red-600 transition-colors" aria-label="Delete">
              <Trash2 className="h-4 w-4" />
            </button>
          )}
        </div>
      </div>

      <div className="text-xs text-gray-400 mb-2">
        v{page.currentVersionNumber} · created by {page.creatorName}
        {page.lastEditedByName && ` · last edited by ${page.lastEditedByName}`}
      </div>

      <WikiPresence viewers={viewers} editLock={editLock} currentUserId={currentUserId} />

      <WikiPageLabels
        apiService={apiService}
        pageId={pageId}
        labels={page.labels || []}
        canWrite={canWrite}
        onChanged={load}
      />

      <WikiContentBlocks
        content={page.contentMarkdown}
        comments={comments}
        canWrite={canWrite}
        currentUserId={currentUserId}
        canManage={canManage}
        onAddAnchoredComment={handleAddAnchoredComment}
        onDeleteComment={handleDeleteAnchoredComment}
      />

      <button
        onClick={() => setShowHistory(!showHistory)}
        className="mt-4 flex items-center text-sm text-gray-500 hover:text-gray-700 transition-colors"
      >
        <Clock className="h-4 w-4 mr-1" />
        {showHistory ? 'Hide version history' : 'Show version history'}
      </button>
      {showHistory && (
        <div className="mt-2">
          <WikiVersionHistory apiService={apiService} pageId={pageId} />
        </div>
      )}

      <div className="mt-6 border-t border-gray-100 pt-4">
        <WikiComments
          apiService={apiService}
          pageId={pageId}
          currentUserId={currentUserId}
          myEffectiveLevel={myEffectiveLevel}
          comments={comments}
          onReload={reloadComments}
        />
      </div>
    </div>
  );
};

export default WikiPageDetail;
