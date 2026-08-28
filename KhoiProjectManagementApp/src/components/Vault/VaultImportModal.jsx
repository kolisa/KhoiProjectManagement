// src/components/Vault/VaultImportModal.js
// Bulk-import Vault entries from a .env/.csv/.json file - see VaultImportParser (backend) for exactly
// what each format accepts. Parsing and encryption both happen server-side; this component only
// uploads the file and renders the resulting per-row summary.
import React, { useState, useRef } from 'react';
import { X, Upload, CheckCircle2, AlertTriangle } from 'lucide-react';
import { reportApiError } from '../../utils/apiError';
import { useToast } from '../../contexts/ToastContext';
import useModalA11y from '../Common/useModalA11y';

const VaultImportModal = ({ apiService, spaceId, onClose, onImported }) => {
  const toast = useToast();
  const modalRef = useModalA11y(onClose);
  const [file, setFile] = useState(null);
  const [importing, setImporting] = useState(false);
  const [result, setResult] = useState(null);
  const fileInputRef = useRef(null);

  const handleImport = async () => {
    if (!file) return;
    setImporting(true);
    try {
      const res = await apiService.importVaultEntries(spaceId, file);
      setResult(res);
      if (res.imported > 0) {
        await onImported();
        toast.success(`Imported ${res.imported} entr${res.imported !== 1 ? 'ies' : 'y'}.`);
      }
    } catch (err) {
      reportApiError(toast, err, 'Could not import this file.');
    } finally {
      setImporting(false);
    }
  };

  return (
    <div className="fixed inset-0 bg-gray-900/50 backdrop-blur-sm flex items-center justify-center p-4 z-50">
      <div ref={modalRef} role="dialog" aria-modal="true" aria-labelledby="vault-import-modal-title" tabIndex={-1} className="bg-white rounded-2xl shadow-xl overflow-hidden w-full max-w-md max-h-[85vh] flex flex-col outline-none">
        <div className="px-6 py-4 border-b border-gray-100 flex items-center justify-between flex-shrink-0">
          <h3 id="vault-import-modal-title" className="text-base font-semibold text-gray-900">Import entries</h3>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600" aria-label="Close">
            <X className="h-4 w-4" />
          </button>
        </div>

        <div className="px-6 py-5 space-y-4 overflow-y-auto">
          {!result ? (
            <>
              <p className="text-sm text-gray-500">
                Upload a <span className="font-mono text-xs bg-gray-100 rounded px-1 py-0.5">.env</span> file
                (<span className="font-mono text-xs">KEY=VALUE</span> per line), a plain{' '}
                <span className="font-mono text-xs bg-gray-100 rounded px-1 py-0.5">.txt</span> note from
                Notepad or Notepad++ (<span className="font-mono text-xs">Label: value</span> per line
                works too), a{' '}
                <span className="font-mono text-xs bg-gray-100 rounded px-1 py-0.5">.csv</span> with a name/secret
                column (username, systemOrUrl, and notes are optional), or a{' '}
                <span className="font-mono text-xs bg-gray-100 rounded px-1 py-0.5">.json</span> array of
                {' '}{'{ name, secret, ... }'} objects. Each row becomes one entry in this category.
              </p>
              <div
                onClick={() => fileInputRef.current?.click()}
                className="border-2 border-dashed border-gray-200 rounded-[10px] p-6 text-center cursor-pointer hover:border-blue-400 hover:bg-blue-50/40 transition-colors"
              >
                <Upload className="h-6 w-6 text-gray-400 mx-auto mb-2" />
                {file ? (
                  <p className="text-sm text-gray-900 font-medium">{file.name}</p>
                ) : (
                  <p className="text-sm text-gray-500">Click to choose a file</p>
                )}
                <input
                  ref={fileInputRef}
                  type="file"
                  accept=".env,.csv,.json,.txt"
                  className="hidden"
                  onChange={(e) => setFile(e.target.files?.[0] || null)}
                />
              </div>
            </>
          ) : (
            <div className="space-y-3">
              <div className="flex items-center gap-2 text-sm">
                <CheckCircle2 className="h-4 w-4 text-green-600 flex-shrink-0" />
                <span className="text-gray-900">{result.imported} imported</span>
                {result.skipped > 0 && (
                  <span className="text-gray-500">&middot; {result.skipped} skipped</span>
                )}
              </div>
              {result.errors?.length > 0 && (
                <div className="bg-amber-50 border border-amber-200 rounded-lg p-3 space-y-1 max-h-48 overflow-y-auto">
                  {result.errors.map((e, i) => (
                    <div key={i} className="flex items-start gap-1.5 text-xs text-amber-800">
                      <AlertTriangle className="h-3 w-3 mt-0.5 flex-shrink-0" />
                      <span>{e}</span>
                    </div>
                  ))}
                </div>
              )}
            </div>
          )}
        </div>

        <div className="px-6 py-4 border-t border-gray-100 flex justify-end gap-3 flex-shrink-0">
          {result ? (
            <button
              onClick={onClose}
              className="inline-flex items-center gap-2 bg-blue-600 text-white px-4 py-2.5 rounded-[10px] text-sm font-semibold hover:bg-blue-700 shadow-sm transition-colors"
            >
              Done
            </button>
          ) : (
            <>
              <button onClick={onClose} className="inline-flex items-center gap-2 bg-white text-gray-700 border border-gray-300 px-4 py-2.5 rounded-[10px] text-sm font-semibold hover:bg-gray-50 transition-colors">
                Cancel
              </button>
              <button
                onClick={handleImport}
                disabled={!file || importing}
                className="inline-flex items-center gap-2 bg-blue-600 text-white px-4 py-2.5 rounded-[10px] text-sm font-semibold hover:bg-blue-700 shadow-sm transition-colors disabled:opacity-50"
              >
                {importing && <span className="h-4 w-4 rounded-full border-2 border-white/40 border-t-white animate-spin" />}
                {importing ? 'Importing...' : 'Import'}
              </button>
            </>
          )}
        </div>
      </div>
    </div>
  );
};

export default VaultImportModal;
