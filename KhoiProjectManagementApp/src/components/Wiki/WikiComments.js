// src/components/Wiki/WikiComments.js
// Flat-rendered-as-threaded list (indent by ParentCommentId depth). Reply/add is gated by Write;
// delete is shown only for the caller's own comments or when the caller holds Manage.
import React, { useState, useEffect } from 'react';
import { Trash2 } from 'lucide-react';
import { hasSpaceLevel } from '../../utils/spaceLevel';

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
  const [replying, setReplying] = useState(false);
  const [replyBody, setReplyBody] = useState('');

  const canDelete = comment.authorId === currentUserId || canManage;

  const submitReply = async (e) => {
    e.preventDefault();
    await onReply(replyBody, comment.id);
    setReplyBody('');
    setReplying(false);
  };

  return (
    <div style={{ marginLeft: depth * 24 }} className="mt-3">
      <div className="bg-gray-50 rounded-lg p-3">
        <div className="flex justify-between items-start">
          <div>
            <span className="font-medium text-sm text-gray-900">{comment.authorName}</span>
            <span className="text-xs text-gray-400 ml-2">{new Date(comment.createdAt).toLocaleString()}</span>
          </div>
          {canDelete && (
            <button onClick={() => onDelete(comment.id)} className="text-gray-400 hover:text-red-600" aria-label="Delete comment">
              <Trash2 className="h-3.5 w-3.5" />
            </button>
          )}
        </div>
        <p className="text-sm text-gray-700 mt-1 whitespace-pre-wrap">{comment.body}</p>
        {canWrite && (
          <button onClick={() => setReplying(!replying)} className="text-xs text-blue-600 hover:text-blue-800 mt-1">
            Reply
          </button>
        )}
        {replying && (
          <form onSubmit={submitReply} className="mt-2 flex space-x-2">
            <input
              type="text"
              value={replyBody}
              onChange={(e) => setReplyBody(e.target.value)}
              className="flex-1 border border-gray-300 rounded px-2 py-1 text-sm"
              placeholder="Write a reply..."
              required
            />
            <button type="submit" className="text-sm bg-blue-600 text-white px-3 py-1 rounded hover:bg-blue-700">
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

const WikiComments = ({ apiService, pageId, currentUserId, myEffectiveLevel }) => {
  const [comments, setComments] = useState(null);
  const [error, setError] = useState(null);
  const [newComment, setNewComment] = useState('');

  const load = async () => {
    try {
      const result = await apiService.getWikiComments(pageId);
      setComments(result || []);
    } catch (err) {
      setError(err.message);
    }
  };

  useEffect(() => {
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [apiService, pageId]);

  const canWrite = hasSpaceLevel(myEffectiveLevel, 'Write');
  const canManage = hasSpaceLevel(myEffectiveLevel, 'Manage');

  const handleReply = async (body, parentCommentId) => {
    await apiService.addWikiComment(pageId, { body, parentCommentId });
    await load();
  };

  const handleAddTopLevel = async (e) => {
    e.preventDefault();
    await apiService.addWikiComment(pageId, { body: newComment });
    setNewComment('');
    await load();
  };

  const handleDelete = async (commentId) => {
    if (!window.confirm('Delete this comment?')) return;
    await apiService.deleteWikiComment(commentId);
    await load();
  };

  if (error) return <div className="text-red-600 text-sm">{error}</div>;
  if (!comments) return <div className="text-gray-400 text-sm">Loading comments...</div>;

  const tree = buildTree(comments);

  return (
    <div>
      <h4 className="font-medium text-gray-900 mb-2">Comments</h4>
      {canWrite && (
        <form onSubmit={handleAddTopLevel} className="flex space-x-2 mb-4">
          <input
            type="text"
            value={newComment}
            onChange={(e) => setNewComment(e.target.value)}
            placeholder="Add a comment..."
            className="flex-1 border border-gray-300 rounded-lg px-3 py-2 text-sm"
            required
          />
          <button type="submit" className="bg-blue-600 text-white px-4 py-2 rounded-lg hover:bg-blue-700 text-sm">
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
