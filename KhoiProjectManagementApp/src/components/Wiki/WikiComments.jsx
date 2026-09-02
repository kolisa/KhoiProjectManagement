// src/components/Wiki/WikiComments.js
// Flat-rendered-as-threaded list (indent by ParentCommentId depth). Reply/add is gated by Write;
// delete is shown only for the caller's own comments or when the caller holds Manage. Comments are
// passed in (not fetched here) so this list and WikiContentBlocks' per-section annotations share one
// source of truth - this component shows only page-level comments (anchorBlockIndex == null);
// anchored ones render inline against their block instead, and are deliberately flat/unthreaded, so
// filtering them out here can never orphan a reply.
import React, { useState } from 'react';
import { Trash2 } from 'lucide-react';
import { hasSpaceLevel } from '../../utils/spaceLevel';
import { useToast } from '../../contexts/ToastContext';
import { useConfirm } from '../../contexts/ConfirmContext';
import { reportApiError } from '../../utils/apiError';

const buildTree = (comments) => {
  const byParent = {};
  comments.forEach((c) => {
    const key = c.parentCommentId || 'root';
    if (!byParent[key]) byParent[key] = [];
    byParent[key].push(c);
  });
  const attach = (parentKey) => (byParent[parentKey] || []).map((c) => ({ ...c, children: attach(c.id) }));
  return attach('root');
};

const CommentNode = ({ comment, currentUserId, canManage, canWrite, onReply, onDelete, depth }) => {
  const toast = useToast();
  const [replying, setReplying] = useState(false);
  const [replyBody, setReplyBody] = useState('');

  const canDelete = comment.authorId === currentUserId || canManage;

  const submitReply = async (e) => {
    e.preventDefault();
    try {
      await onReply(replyBody, comment.id);
      setReplyBody('');
      setReplying(false);
    } catch (err) {
      reportApiError(toast, err, 'Could not post reply.');
    }
  };

  return (
    <div style={{ marginLeft: depth * 24 }} className="mt-3">
      <div className="bg-gray-50 rounded-lg p-3 border border-gray-100">
        <div className="flex justify-between items-start">
          <div>
            <span className="font-medium text-sm text-gray-900">{comment.authorName}</span>
            <span className="text-xs text-gray-400 ml-2">{new Date(comment.createdAt).toLocaleString()}</span>
          </div>
          {canDelete && (
            <button onClick={() => onDelete(comment.id)} className="text-gray-400 hover:text-red-600 hover:bg-red-50 rounded-md p-0.5 transition-colors" aria-label="Delete comment">
              <Trash2 className="h-3.5 w-3.5" />
            </button>
          )}
        </div>
        <p className="text-sm text-gray-700 mt-1 whitespace-pre-wrap">{comment.body}</p>
        {canWrite && (
          <button onClick={() => setReplying(!replying)} className="text-xs font-semibold text-blue-600 hover:text-blue-800 hover:bg-blue-50 rounded-md px-1.5 py-1 -ml-1.5 mt-1 transition-colors">
            Reply
          </button>
        )}
        {replying && (
          <form onSubmit={submitReply} className="mt-2 flex space-x-2">
            <input
              type="text"
              value={replyBody}
              onChange={(e) => setReplyBody(e.target.value)}
              className="flex-1 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow"
              placeholder="Write a reply..."
              required
            />
            <button type="submit" className="inline-flex items-center bg-blue-600 text-white px-3.5 py-2 rounded-lg text-sm font-semibold hover:bg-blue-700 shadow-sm transition-colors">
              Send
            </button>
          </form>
        )}
      </div>
      {comment.children.map((child) => (
        <CommentNode
          key={child.id}
          comment={child}
          currentUserId={currentUserId}
          canManage={canManage}
          canWrite={canWrite}
          onReply={onReply}
          onDelete={onDelete}
          depth={depth + 1}
        />
      ))}
    </div>
  );
};

const WikiComments = ({ apiService, pageId, currentUserId, myEffectiveLevel, comments, onReload }) => {
  const toast = useToast();
  const confirm = useConfirm();
  const [newComment, setNewComment] = useState('');

  const canWrite = hasSpaceLevel(myEffectiveLevel, 'Write');
  const canManage = hasSpaceLevel(myEffectiveLevel, 'Manage');

  // Not try/caught - CommentNode's submitReply above needs the rejection to keep its reply box open
  // with the typed text intact rather than silently clearing it on failure.
  const handleReply = async (body, parentCommentId) => {
    await apiService.addWikiComment(pageId, { body, parentCommentId });
    await onReload();
  };

  const handleAddTopLevel = async (e) => {
    e.preventDefault();
    try {
      await apiService.addWikiComment(pageId, { body: newComment });
      setNewComment('');
      await onReload();
    } catch (err) {
      reportApiError(toast, err, 'Could not post comment.');
    }
  };

  const handleDelete = async (commentId) => {
    if (!(await confirm('Delete this comment?', { title: 'Delete comment', confirmText: 'Delete', danger: true }))) return;
    try {
      await apiService.deleteWikiComment(commentId);
      await onReload();
    } catch (err) {
      reportApiError(toast, err, 'Could not delete comment.');
    }
  };

  const pageLevelComments = comments.filter((c) => c.anchorBlockIndex === null || c.anchorBlockIndex === undefined);
  const tree = buildTree(pageLevelComments);

  return (
    <div>
      <h4 className="text-base font-semibold text-gray-900 mb-2">Comments</h4>
      {canWrite && (
        <form onSubmit={handleAddTopLevel} className="flex space-x-2 mb-4">
          <input
            type="text"
            value={newComment}
            onChange={(e) => setNewComment(e.target.value)}
            placeholder="Add a comment..."
            className="flex-1 border border-gray-300 rounded-[10px] px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow"
            required
          />
          <button type="submit" className="inline-flex items-center gap-2 bg-blue-600 text-white px-4 py-2.5 rounded-[10px] text-sm font-semibold hover:bg-blue-700 shadow-sm transition-colors">
            Comment
          </button>
        </form>
      )}
      {tree.length === 0 && <div className="text-gray-400 text-sm italic">No comments yet.</div>}
      {tree.map((comment) => (
        <CommentNode
          key={comment.id}
          comment={comment}
          currentUserId={currentUserId}
          canManage={canManage}
          canWrite={canWrite}
          onReply={handleReply}
          onDelete={handleDelete}
          depth={0}
        />
      ))}
    </div>
  );
};

export default WikiComments;
