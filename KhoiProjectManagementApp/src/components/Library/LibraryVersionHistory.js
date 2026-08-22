// src/components/Library/LibraryVersionHistory.js
import React, { useState, useEffect } from 'react';
import { Download } from 'lucide-react';
import { formatFileSize } from '../../utils/formatFileSize';

const LibraryVersionHistory = ({ apiService, file }) => {
  const [versions, setVersions] = useState(null);
  const [error, setError] = useState(null);

  useEffect(() => {
    const load = async () => {
      try {
        const result = await apiService.getLibraryFileVersions(file.id);
        setVersions(result || []);
      } catch (err) {
        setError(err.message);
      }
    };
    load();
  }, [apiService, file.id]);

  const handleDownloadVersion = async (versionNumber) => {
    try {
      await apiService.downloadLibraryFileVersion(file.id, versionNumber, file.fileName);
    } catch (err) {
      setError(err.message);
    }
  };

  if (error) return <div className="text-red-600 text-sm">{error}</div>;
  if (!versions) return <div className="text-gray-400 text-sm">Loading version history...</div>;

  return (
    <div className="divide-y border rounded-lg">
      {versions.map((v) => (
        <div key={v.versionNumber} className="p-3 flex justify-between items-center text-sm">
          <div>
            <span className="font-medium">v{v.versionNumber}</span> by {v.uploadedByName} ·{' '}
            {formatFileSize(v.fileSize)}
            {v.comment && <span className="text-gray-500"> — {v.comment}</span>}
            <div className="text-gray-400">{new Date(v.uploadedAt).toLocaleString()}</div>
          </div>
          <button
            onClick={() => handleDownloadVersion(v.versionNumber)}
            className="text-blue-600 hover:text-blue-800"
            aria-label="Download this version"
          >
            <Download className="h-4 w-4" />
          </button>
        </div>
      ))}
    </div>
  );
};

export default LibraryVersionHistory;
