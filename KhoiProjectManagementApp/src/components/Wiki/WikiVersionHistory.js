// src/components/Wiki/WikiVersionHistory.js
import React, { useState, useEffect } from 'react';
import ReactMarkdown from 'react-markdown';

const WikiVersionHistory = ({ apiService, pageId }) => {
  const [versions, setVersions] = useState(null);
  const [error, setError] = useState(null);
  const [selectedVersion, setSelectedVersion] = useState(null);

  useEffect(() => {
    const load = async () => {
      try {
        const result = await apiService.getWikiVersions(pageId);
        setVersions(result || []);
      } catch (err) {
        setError(err.message);
      }
    };
    load();
  }, [apiService, pageId]);

  const handleViewVersion = async (versionNumber) => {
    try {
      const result = await apiService.getWikiVersion(pageId, versionNumber);
      setSelectedVersion(result);
    } catch (err) {
      setError(err.message);
    }
  };

  if (error) return <div className="text-red-600 text-sm">{error}</div>;
  if (!versions) return <div className="text-gray-400 text-sm">Loading version history...</div>;

  return (
    <div className="space-y-3">
      <div className="divide-y border rounded-lg">
        {versions.map((v) => (
          <div
            key={v.versionNumber}
            onClick={() => handleViewVersion(v.versionNumber)}
            className="p-3 hover:bg-gray-50 cursor-pointer flex justify-between items-center text-sm"
          >
            <div>
              <span className="font-medium">v{v.versionNumber}</span> by {v.editedByName}
              {v.editSummary && <span className="text-gray-500"> — {v.editSummary}</span>}
            </div>
            <div className="text-gray-400">{new Date(v.editedAt).toLocaleString()}</div>
          </div>
        ))}
      </div>

      {selectedVersion && (
        <div className="border rounded-lg p-4 bg-gray-50">
          <div className="text-sm text-gray-500 mb-2">
            Version {selectedVersion.versionNumber} — {new Date(selectedVersion.editedAt).toLocaleString()}
          </div>
          <div className="prose prose-sm max-w-none">
            <ReactMarkdown>{selectedVersion.contentMarkdown}</ReactMarkdown>
          </div>
        </div>
      )}
    </div>
  );
};

export default WikiVersionHistory;
