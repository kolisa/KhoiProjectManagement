// src/components/Ideas/IdeaDetail.js
import React, { useState, useEffect, useRef } from 'react';
import { X, Upload, Download, Trash2, MessageSquare, FileImage, ArrowRightCircle } from 'lucide-react';
import { hasPermission } from '../../utils/permissions';
import { formatFileSize } from '../../utils/formatFileSize';
import IdeaAttachmentAnnotations from './IdeaAttachmentAnnotations';

const STATUS_OPTIONS = ['Submitted', 'UnderReview', 'Approved', 'Rejected'];

const IdeaDetail = ({ apiService, user, ideaId, onClose, onChanged }) => {
  const [idea, setIdea] = useState(null);
  const [comments, setComments] = useState([]);
  const [attachments, setAttachments] = useState([]);
  const [expandedAttachmentId, setExpandedAttachmentId] = useState(null);
  const [commentBody, setCommentBody] = useState('');
  const [error, setError] = useState(null);
  const [uploading, setUploading] = useState(false);
  const [converting, setConverting] = useState(false);
  const fileInputRef = useRef(null);

  const canManage = hasPermission(user?.permissions, 'ideas.manage');

  const load = async () => {
    try {
      const [ideaResult, commentsResult, attachmentsResult] = await Promise.all([
        apiService.getIdea(ideaId),
        apiService.getIdeaComments(ideaId),
        apiService.getIdeaAttachments(ideaId),
      ]);
      setIdea(ideaResult);
      setComments(commentsResult || []);
      setAttachments(attachmentsResult || []);
    } catch (err) {
      setError(err.message);
    }
  };

  useEffect(() => {
    load();
    setExpandedAttachmentId(null);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [ideaId]);

  const handleAddComment = async () => {
    if (!commentBody.trim()) return;
    try {
      await apiService.addIdeaComment(ideaId, { body: commentBody });
      setCommentBody('');
      const result = await apiService.getIdeaComments(ideaId);
      setComments(result || []);
    } catch (err) {
      setError(err.message);
    }
  };

  const handleDeleteComment = async (commentId) => {
    try {
      await apiService.deleteIdeaComment(commentId);
      const result = await apiService.getIdeaComments(ideaId);
      setComments(result || []);
    } catch (err) {
      setError(err.message);
    }
  };

  const handleUpload = async (e) => {
    const file = e.target.files?.[0];
    e.target.value = '';
    if (!file) return;
    setUploading(true);
    try {
      await apiService.uploadIdeaAttachment(ideaId, file);
      const result = await apiService.getIdeaAttachments(ideaId);
      setAttachments(result || []);
    } catch (err) {
      setError(err.message);
    } finally {
      setUploading(false);
    }
  };

  const handleDownload = async (attachment) => {
    try {
      await apiService.downloadIdeaAttachment(attachment.id, attachment.originalFileName);
    } catch (err) {
      setError(err.message);
    }
  };

  const handleDeleteAttachment = async (attachmentId) => {
    if (!window.confirm('Delete this file?')) return;
    try {
      await apiService.deleteIdeaAttachment(attachmentId);
      const result = await apiService.getIdeaAttachments(ideaId);
      setAttachments(result || []);
    } catch (err) {
      setError(err.message);
    }
  };

  const handleStatusChange = async (status) => {
    try {
      await apiService.updateIdeaStatus(ideaId, status);
      await load();
      onChanged();
    } catch (err) {
      setError(err.message);
    }
  };

  const handleConvert = async () => {
    if (!window.confirm(`Convert "${idea.title}" into a project?`)) return;
    setConverting(true);
    try {
      await apiService.convertIdeaToProject(ideaId);
      await load();
      onChanged();
    } catch (err) {
      setError(err.message);
    } finally {
      setConverting(false);
    }
  };

  if (error && !idea) return <div className="p-4 text-red-600">Error: {error}</div>;
  if (!idea) return <div className="p-4 text-gray-400">Loading...</div>;

  return (
    <div className="bg-white rounded-xl border border-gray-100 shadow-sm p-6">
      <div className="flex justify-between items-start mb-2">
        <div>
          <h3 className="text-xl font-semibold text-gray-900">{idea.title}</h3>
          <p className="text-xs text-gray-500">
            Submitted by {idea.submitterName} on {new Date(idea.createdAt).toLocaleDateString()}
          </p>
        </div>
        <button onClick={onClose} className="text-gray-400 hover:text-gray-600" aria-label="Close">
          <X className="h-4 w-4" />
        </button>
      </div>

      {error && <div className="text-red-600 text-sm mb-2">{error}</div>}

      <p className="text-gray-700 my-4">{idea.description}</p>

      <div className="flex items-center flex-wrap gap-3 mb-6">
        <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-semibold bg-blue-50 text-blue-700">
          <span className="w-1.5 h-1.5 rounded-full bg-blue-500" />
          {idea.status}
        </span>

        {canManage && idea.status !== 'ConvertedToProject' && (
          <select
            value={idea.status}
            onChange={(e) => handleStatusChange(e.target.value)}
            className="border border-gray-300 rounded-lg px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow"
          >
            {STATUS_OPTIONS.map((s) => (
              <option key={s} value={s}>{s}</option>
            ))}
          </select>
        )}

        {canManage && idea.status !== 'ConvertedToProject' && (
          <button
            onClick={handleConvert}
            disabled={converting}
            className="inline-flex items-center gap-2 text-sm bg-green-600 text-white px-3 py-1.5 rounded-lg hover:bg-green-700 disabled:opacity-50 transition-colors shadow-sm"
          >
            <ArrowRightCircle className="h-4 w-4" />
            {converting ? 'Converting...' : 'Convert to Project'}
          </button>
        )}

        {idea.status === 'ConvertedToProject' && (
          <span className="text-sm text-green-700">Converted to project: {idea.convertedProjectName}</span>
        )}
      </div>

      {/* Attachments */}
      <div className="border-t border-gray-100 pt-4 mb-6">
        <div className="flex justify-between items-center mb-2">
          <h4 className="font-medium text-gray-900 flex items-center">
            <FileImage className="h-4 w-4 mr-1" />
            Prototypes &amp; Mockups
          </h4>
          <button
            onClick={() => fileInputRef.current?.click()}
            disabled={uploading}
            className="flex items-center text-sm text-blue-600 hover:text-blue-800 disabled:opacity-50"
          >
            <Upload className="h-4 w-4 mr-1" />
            {uploading ? 'Uploading...' : 'Upload File'}
          </button>
          <input ref={fileInputRef} type="file" className="hidden" onChange={handleUpload} />
        </div>

        {attachments.length === 0 && <div className="text-sm text-gray-400 italic">No files uploaded yet.</div>}

        <div className="divide-y border border-gray-200 rounded-lg">
          {attachments.map((a) => (
            <div key={a.id}>
              <div className="p-3 flex justify-between items-center">
                <div>
                  <div className="text-sm font-medium text-gray-900">{a.originalFileName}</div>
                  <div className="text-xs text-gray-500">
                    {formatFileSize(a.fileSize)} &middot; {a.uploaderName} &middot; {a.annotationCount} annotation{a.annotationCount !== 1 ? 's' : ''}
                  </div>
                </div>
                <div className="flex items-center space-x-3">
                  <button onClick={() => handleDownload(a)} className="text-blue-600 hover:text-blue-800" aria-label="Download">
                    <Download className="h-4 w-4" />
                  </button>
                  <button
                    onClick={() => setExpandedAttachmentId(expandedAttachmentId === a.id ? null : a.id)}
                    className="text-xs text-gray-500 hover:text-gray-700"
                  >
                    {expandedAttachmentId === a.id ? 'Hide annotations' : 'Annotate'}
                  </button>
                  {(a.uploadedBy === user?.id || canManage) && (
                    <button onClick={() => handleDeleteAttachment(a.id)} className="text-red-400 hover:text-red-600" aria-label="Delete file">
                      <Trash2 className="h-4 w-4" />
                    </button>
                  )}
                </div>
              </div>
              {expandedAttachmentId === a.id && (
                <IdeaAttachmentAnnotations
                  apiService={apiService}
                  attachmentId={a.id}
                  currentUserId={user?.id}
                  canManage={canManage}
                  onAnnotationsChanged={async () => {
                    const result = await apiService.getIdeaAttachments(ideaId);
                    setAttachments(result || []);
                  }}
                />
              )}
            </div>
          ))}
        </div>
      </div>

      {/* Comments */}
      <div className="border-t border-gray-100 pt-4">
        <h4 className="font-medium text-gray-900 flex items-center mb-2">
          <MessageSquare className="h-4 w-4 mr-1" />
          Discussion
        </h4>
        <div className="flex space-x-2 mb-3">
          <input
            type="text"
            value={commentBody}
            onChange={(e) => setCommentBody(e.target.value)}
            onKeyDown={(e) => { if (e.key === 'Enter') handleAddComment(); }}
            placeholder="Add a comment... (use @Name to mention someone)"
            className="flex-1 border border-gray-300 rounded-lg px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow"
          />
          <button
            onClick={handleAddComment}
            disabled={!commentBody.trim()}
            className="inline-flex items-center gap-2 bg-blue-600 text-white px-4 py-2.5 rounded-lg text-sm font-semibold hover:bg-blue-700 shadow-sm transition-colors disabled:opacity-50"
          >
            Comment
          </button>
        </div>
        <div className="space-y-2">
          {comments.map((c) => (
            <div key={c.id} className="flex justify-between items-start text-sm bg-gray-50 rounded-lg p-3">
              <div>
                <span className="font-medium text-gray-900">{c.authorName}</span>{' '}
                <span className="text-xs text-gray-400">{new Date(c.createdAt).toLocaleString()}</span>
                <p className="text-gray-700 mt-0.5">{c.body}</p>
              </div>
              {(c.authoredBy === user?.id || canManage) && (
                <button onClick={() => handleDeleteComment(c.id)} className="text-red-400 hover:text-red-600 flex-shrink-0 ml-2" aria-label="Delete comment">
                  <Trash2 className="h-3.5 w-3.5" />
                </button>
              )}
            </div>
          ))}
        </div>
      </div>
    </div>
  );
};

export default IdeaDetail;
