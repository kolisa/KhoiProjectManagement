// src/components/Wiki/WikiContentBlocks.js
// Renders page content split into blocks (paragraphs/headings/lists, split on blank lines) instead
// of one opaque blob, so a comment can anchor to a specific block instead of only the whole page.
// Anchored comments are deliberately flat (no replies) - keeps "which comment shows where" simple
// and unambiguous; threaded discussion stays in WikiComments' page-level Discussion section.
import React, { useState } from 'react';
import ReactMarkdown from 'react-markdown';
import { MessageSquarePlus, Trash2 } from 'lucide-react';

export const splitBlocks = (markdown) =>
  (markdown || '').split(/\n\s*\n/).filter((b) => b.trim().length > 0);

const WikiContentBlocks = ({ content, comments, canWrite, currentUserId, canManage, onAddAnchoredComment, onDeleteComment }) => {
  const [commentingIndex, setCommentingIndex] = useState(null);
  const [draftBody, setDraftBody] = useState('');

  const blocks = splitBlocks(content);

  const commentsByBlock = {};
  comments
    .filter((c) => c.anchorBlockIndex !== null && c.anchorBlockIndex !== undefined)
    .forEach((c) => {
      if (!commentsByBlock[c.anchorBlockIndex]) commentsByBlock[c.anchorBlockIndex] = [];
      commentsByBlock[c.anchorBlockIndex].push(c);
    });

  const handleSubmit = async (index) => {
    if (!draftBody.trim()) return;
    await onAddAnchoredComment(draftBody, index, blocks[index].slice(0, 200));
    setDraftBody('');
    setCommentingIndex(null);
  };

  return (
    <div className="prose prose-sm max-w-none">
      {blocks.map((block, i) => (
        <div key={i} className="group relative rounded hover:bg-blue-50/50 -mx-2 px-2">
          <ReactMarkdown>{block}</ReactMarkdown>

          {canWrite && (
            <button
              onClick={() => { setCommentingIndex(commentingIndex === i ? null : i); setDraftBody(''); }}
              className="opacity-0 group-hover:opacity-100 absolute -left-6 top-1 text-gray-300 hover:text-blue-600 hover:bg-blue-50 rounded-md p-0.5 transition-all"
              aria-label="Comment on this section"
              title="Comment on this section"
            >
              <MessageSquarePlus className="h-4 w-4" />
            </button>
          )}

          {(commentsByBlock[i] || []).length > 0 && (
            <div className="not-prose ml-2 my-2 space-y-1 border-l-2 border-blue-200 pl-3">
              {commentsByBlock[i].map((c) => (
                <div key={c.id} className="text-xs bg-blue-50 rounded-lg p-2 flex justify-between items-start">
                  <div>
                    <span className="font-medium text-gray-900">{c.authorName}</span>{' '}
                    <span className="text-gray-400">{new Date(c.createdAt).toLocaleString()}</span>
                    <p className="text-gray-700 mt-0.5">{c.body}</p>
                  </div>
                  {(c.authorId === currentUserId || canManage) && (
                    <button
                      onClick={() => onDeleteComment(c.id)}
                      className="text-gray-400 hover:text-red-600 hover:bg-red-50 rounded-md p-0.5 flex-shrink-0 ml-2 transition-colors"
                      aria-label="Delete comment"
                    >
                      <Trash2 className="h-3 w-3" />
                    </button>
                  )}
                </div>
              ))}
            </div>
          )}

          {commentingIndex === i && (
            <div className="not-prose ml-2 my-2 flex space-x-2">
              <input
                type="text"
                autoFocus
                value={draftBody}
                onChange={(e) => setDraftBody(e.target.value)}
                onKeyDown={(e) => { if (e.key === 'Enter') handleSubmit(i); }}
                placeholder="Comment on this section..."
                className="flex-1 border border-gray-300 rounded-lg px-2.5 py-1.5 text-xs focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow"
              />
              <button onClick={() => handleSubmit(i)} className="inline-flex items-center text-xs bg-blue-600 text-white px-3 py-1.5 rounded-lg font-semibold hover:bg-blue-700 shadow-sm transition-colors">
                Add
              </button>
              <button onClick={() => { setCommentingIndex(null); setDraftBody(''); }} className="text-xs text-gray-500 hover:text-gray-700 hover:bg-gray-100 rounded-md px-2 py-1.5 transition-colors">
                Cancel
              </button>
            </div>
          )}
        </div>
      ))}
    </div>
  );
};

export default WikiContentBlocks;
