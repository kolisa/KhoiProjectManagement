// src/components/Ideas/IdeaAttachmentAnnotations.js
// Short notes tied to one specific prototype/mockup file - distinct from the idea's own comment
// thread. Flat list, not pin/coordinate markup on the image (see plan addendum).
import React, { useState, useEffect } from 'react';
import { Trash2 } from 'lucide-react';
import { useToast } from '../../contexts/ToastContext';
import { reportApiError } from '../../utils/apiError';

const IdeaAttachmentAnnotations = ({ apiService, attachmentId, currentUserId, canManage, onAnnotationsChanged }) => {
  const toast = useToast();
  const [annotations, setAnnotations] = useState(null);
  const [body, setBody] = useState('');
  const [error, setError] = useState(null);
  const [posting, setPosting] = useState(false);

  const load = async () => {
    try {
      const result = await apiService.getIdeaAttachmentAnnotations(attachmentId);
      setAnnotations(result || []);
    } catch (err) {
      setError(err.message);
    }
  };

  useEffect(() => {
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [attachmentId]);

  const handleAdd = async () => {
    if (!body.trim()) return;
    setPosting(true);
    try {
      await apiService.addIdeaAttachmentAnnotation(attachmentId, { body });
      setBody('');
      await load();
      onAnnotationsChanged?.();
    } catch (err) {
      reportApiError(toast, err, 'Could not post annotation.');
    } finally {
      setPosting(false);
    }
  };

  const handleDelete = async (id) => {
    try {
      await apiService.deleteIdeaAttachmentAnnotation(id);
      await load();
      onAnnotationsChanged?.();
    } catch (err) {
      reportApiError(toast, err, 'Could not delete annotation.');
    }
  };

  if (!annotations) return <div className="text-xs text-gray-400 p-2">Loading annotations...</div>;

  return (
    <div className="bg-gray-50 p-3 space-y-2">
      {error && <div className="text-xs text-red-600">{error}</div>}
      {annotations.length === 0 && <div className="text-xs text-gray-400 italic">No annotations on this file yet.</div>}
      {annotations.map((a) => (
        <div key={a.id} className="flex justify-between items-start text-xs bg-white rounded p-2 border">
          <div>
            <span className="font-medium text-gray-900">{a.authorName}</span>{' '}
            <span className="text-gray-400">{new Date(a.createdAt).toLocaleString()}</span>
            <p className="text-gray-700 mt-0.5">{a.body}</p>
          </div>
          {(a.authoredBy === currentUserId || canManage) && (
            <button onClick={() => handleDelete(a.id)} className="text-red-400 hover:text-red-600 flex-shrink-0 ml-2" aria-label="Delete annotation">
              <Trash2 className="h-3.5 w-3.5" />
            </button>
          )}
        </div>
      ))}
      <div className="flex space-x-2">
        <input
          type="text"
          value={body}
          onChange={(e) => setBody(e.target.value)}
          onKeyDown={(e) => { if (e.key === 'Enter') handleAdd(); }}
          placeholder="Add an annotation on this file..."
          className="flex-1 border border-gray-300 rounded-[10px] px-3.5 py-2.5 text-xs focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow"
        />
        <button
          onClick={handleAdd}
          disabled={posting || !body.trim()}
          className="inline-flex items-center gap-2 text-xs font-semibold bg-blue-600 text-white px-3 py-2 rounded-lg hover:bg-blue-700 shadow-sm transition-colors disabled:opacity-50"
        >
          Add
        </button>
      </div>
    </div>
  );
};

export default IdeaAttachmentAnnotations;
