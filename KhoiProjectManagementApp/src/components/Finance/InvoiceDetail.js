// src/components/Finance/InvoiceDetail.js
import React, { useState, useRef } from 'react';
import { Upload, Download, X } from 'lucide-react';
import { hasPermission } from '../../utils/permissions';

const InvoiceDetail = ({ apiService, user, invoice, onClose, onChanged }) => {
  const [error, setError] = useState(null);
  const [uploading, setUploading] = useState(false);
  const [showTemplatePrompt, setShowTemplatePrompt] = useState(false);
  const [templateName, setTemplateName] = useState('');
  const fileInputRef = useRef(null);

  const canManage = hasPermission(user?.permissions, 'finance.manage');

  const handleUpload = async (e) => {
    const file = e.target.files?.[0];
    e.target.value = '';
    if (!file) return;
    setUploading(true);
    try {
      const result = await apiService.uploadInvoiceFile(invoice.id, file);
      await onChanged();
      // The backend only suggests this the first time a look is uploaded, and never for an invoice
      // that already came from a template - see InvoiceService.UploadFileAsync.
      if (result?.suggestSaveAsTemplate) {
        setTemplateName(`${invoice.clientName} Layout`);
        setShowTemplatePrompt(true);
      }
    } catch (err) {
      setError(err.message);
    } finally {
      setUploading(false);
    }
  };

  const handleDownload = async () => {
    try {
      await apiService.downloadInvoiceFile(invoice.id, invoice.originalFileName);
    } catch (err) {
      setError(err.message);
    }
  };

  const handleSaveTemplate = async () => {
    try {
      await apiService.saveInvoiceAsTemplate(invoice.id, { name: templateName, clientName: invoice.clientName });
      setShowTemplatePrompt(false);
      await onChanged();
    } catch (err) {
      setError(err.message);
    }
  };

  return (
    <div className="bg-white rounded-lg shadow p-6">
      <div className="flex justify-between items-start mb-4">
        <div>
          <h3 className="text-lg font-semibold text-gray-900">{invoice.invoiceNumber}</h3>
          <p className="text-sm text-gray-500">{invoice.clientName}</p>
        </div>
        <button onClick={onClose} className="text-gray-400 hover:text-gray-600" aria-label="Close">
          <X className="h-4 w-4" />
        </button>
      </div>

      {error && <div className="text-red-600 text-sm mb-3">{error}</div>}

      <dl className="space-y-2 text-sm mb-4">
        <div>
          <dt className="text-gray-500">Status</dt>
          <dd className="text-gray-900">{invoice.status}</dd>
        </div>
        <div>
          <dt className="text-gray-500">Line items</dt>
          <dd>
            <table className="w-full text-sm mt-1">
              <tbody>
                {invoice.lineItems.map((li) => (
                  <tr key={li.id} className="border-t">
                    <td className="py-1">{li.description}</td>
                    <td className="py-1 text-right">{li.quantity} &times; {li.unitPrice}</td>
                    <td className="py-1 text-right">{(li.quantity * li.unitPrice).toFixed(2)}</td>
                  </tr>
                ))}
                <tr className="border-t font-semibold">
                  <td className="py-1" colSpan={2}>Total</td>
                  <td className="py-1 text-right">{invoice.total?.toFixed(2)}</td>
                </tr>
              </tbody>
            </table>
          </dd>
        </div>
      </dl>

      <div className="flex items-center space-x-3">
        {invoice.originalFileName ? (
          <button onClick={handleDownload} className="flex items-center text-blue-600 hover:text-blue-800 text-sm">
            <Download className="h-4 w-4 mr-1" />
            {invoice.originalFileName}
          </button>
        ) : (
          <span className="text-sm text-gray-400 italic">No document attached</span>
        )}

        {canManage && (
          <>
            <button
              onClick={() => fileInputRef.current?.click()}
              disabled={uploading}
              className="flex items-center text-sm text-gray-600 hover:text-gray-900 disabled:opacity-50"
            >
              <Upload className="h-4 w-4 mr-1" />
              {uploading ? 'Uploading...' : invoice.originalFileName ? 'Replace document' : 'Upload document'}
            </button>
            <input ref={fileInputRef} type="file" className="hidden" onChange={handleUpload} />
          </>
        )}
      </div>

      {showTemplatePrompt && (
        <div className="mt-4 border-t pt-4">
          <p className="text-sm text-gray-700 mb-2">
            Save this document's look as a reusable template, so future invoices for this client can start from it?
          </p>
          <input
            type="text"
            value={templateName}
            onChange={(e) => setTemplateName(e.target.value)}
            className="w-full border rounded-lg px-3 py-2 mb-2 text-sm"
            placeholder="Template name"
          />
          <div className="flex justify-end space-x-2">
            <button
              onClick={() => setShowTemplatePrompt(false)}
              className="px-3 py-1.5 text-sm rounded-lg text-gray-600 hover:bg-gray-100"
            >
              No thanks
            </button>
            <button
              onClick={handleSaveTemplate}
              disabled={!templateName.trim()}
              className="px-3 py-1.5 text-sm rounded-lg bg-blue-600 text-white hover:bg-blue-700 disabled:opacity-50"
            >
              Save as template
            </button>
          </div>
        </div>
      )}
    </div>
  );
};

export default InvoiceDetail;
