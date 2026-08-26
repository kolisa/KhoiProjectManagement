// src/components/Finance/InvoiceDetail.js
import React, { useState, useRef } from 'react';
import { Upload, Download, X } from 'lucide-react';
import { hasPermission } from '../../utils/permissions';
import { useToast } from '../../contexts/ToastContext';
import { reportApiError } from '../../utils/apiError';
import { formatCurrency } from '../../utils/currency';

const InvoiceStatusBadge = ({ status }) => (
  <span className={`inline-flex items-center px-[9px] py-[3px] rounded-[7px] text-[11.5px] font-semibold whitespace-nowrap ${
    status === 'Paid' ? 'bg-[#E3F8E9] text-[#005F2E]' :
    status === 'Sent' ? 'bg-[#FFEED6] text-[#874400]' :
    status === 'Overdue' ? 'bg-[#FFEBE8] text-[#B71824]' :
    'bg-[#F2F2F4] text-[#62626A]'
  }`}>
    {status}
  </span>
);

const InvoiceDetail = ({ apiService, user, invoice, onClose, onChanged }) => {
  const toast = useToast();
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
      toast.success('Document uploaded.');
      // The backend only suggests this the first time a look is uploaded, and never for an invoice
      // that already came from a template - see InvoiceService.UploadFileAsync.
      if (result?.suggestSaveAsTemplate) {
        setTemplateName(`${invoice.clientName} Layout`);
        setShowTemplatePrompt(true);
      }
    } catch (err) {
      reportApiError(toast, err, 'Could not upload this document.');
    } finally {
      setUploading(false);
    }
  };

  const handleDownload = async () => {
    try {
      await apiService.downloadInvoiceFile(invoice.id, invoice.originalFileName);
    } catch (err) {
      reportApiError(toast, err, 'Could not download this document.');
    }
  };

  const handleSaveTemplate = async () => {
    try {
      await apiService.saveInvoiceAsTemplate(invoice.id, { name: templateName, clientName: invoice.clientName });
      setShowTemplatePrompt(false);
      await onChanged();
      toast.success('Template saved.');
    } catch (err) {
      reportApiError(toast, err, 'Could not save this template.');
    }
  };

  const handleStatusChange = async (status) => {
    try {
      await apiService.updateInvoiceStatus(invoice.id, status);
      await onChanged();
      toast.success(`Marked as ${status}.`);
    } catch (err) {
      reportApiError(toast, err, 'Could not update invoice status.');
    }
  };

  return (
    <div className="bg-white rounded-2xl border border-gray-100 shadow-sm p-6">
      <div className="flex justify-between items-start mb-4">
        <div>
          <h3 className="font-mono text-base font-semibold text-gray-900">{invoice.invoiceNumber}</h3>
          <p className="text-sm text-gray-500">{invoice.clientName}</p>
        </div>
        <button onClick={onClose} className="text-gray-400 hover:text-gray-600" aria-label="Close">
          <X className="h-4 w-4" />
        </button>
      </div>

      <dl className="space-y-2 text-sm mb-4">
        <div>
          <dt className="text-gray-500">Status</dt>
          <dd className="text-gray-900 mt-1 flex items-center gap-2">
            <InvoiceStatusBadge status={invoice.status} />
            {canManage && (
              <select
                value=""
                onChange={(e) => e.target.value && handleStatusChange(e.target.value)}
                className="text-xs border border-gray-300 rounded-md px-2 py-1 text-gray-500 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
              >
                <option value="">Change status...</option>
                {['Draft', 'Sent', 'Paid', 'Overdue'].filter((s) => s !== invoice.status).map((s) => (
                  <option key={s} value={s}>{s}</option>
                ))}
              </select>
            )}
          </dd>
        </div>
        <div>
          <dt className="text-gray-500">Line items</dt>
          <dd>
            <table className="w-full text-sm mt-1">
              <tbody>
                {invoice.lineItems.map((li) => (
                  <tr key={li.id} className="border-t">
                    <td className="py-1">{li.description}</td>
                    <td className="py-1 text-right">{li.quantity} &times; {formatCurrency(li.unitPrice)}</td>
                    <td className="py-1 text-right">{formatCurrency(li.quantity * li.unitPrice)}</td>
                  </tr>
                ))}
                <tr className="border-t font-semibold">
                  <td className="py-1" colSpan={2}>Total</td>
                  <td className="py-1 text-right">{formatCurrency(invoice.total)}</td>
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
            className="w-full border border-gray-300 rounded-[10px] px-3.5 py-2.5 mb-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow"
            placeholder="Template name"
          />
          <div className="flex justify-end gap-3">
            <button
              onClick={() => setShowTemplatePrompt(false)}
              className="inline-flex items-center gap-2 bg-white text-gray-700 border border-gray-300 px-3 py-1.5 rounded-lg text-sm font-semibold hover:bg-gray-50 transition-colors"
            >
              No thanks
            </button>
            <button
              onClick={handleSaveTemplate}
              disabled={!templateName.trim()}
              className="inline-flex items-center gap-2 bg-blue-600 text-white px-3 py-1.5 rounded-lg text-sm font-semibold hover:bg-blue-700 shadow-sm transition-colors disabled:opacity-50"
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
