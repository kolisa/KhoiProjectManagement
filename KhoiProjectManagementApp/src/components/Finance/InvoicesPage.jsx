// src/components/Finance/InvoicesPage.js
import React, { useState, useEffect } from 'react';
import { Plus, FileStack, X } from 'lucide-react';
import RandIcon from '../Common/RandIcon';
import { hasPermission } from '../../utils/permissions';
import InvoiceDetail from './InvoiceDetail';
import { useToast } from '../../contexts/ToastContext';
import { reportApiError } from '../../utils/apiError';
import { formatCurrency } from '../../utils/currency';
import { validateInvoice, hasErrors } from '../../utils/validation';

const emptyLineItem = () => ({ description: '', quantity: 1, unitPrice: 0 });

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

const InvoiceFormModal = ({ title, templates, onSave, onClose }) => {
  const [invoiceNumber, setInvoiceNumber] = useState('');
  const [clientName, setClientName] = useState('');
  const [issueDate, setIssueDate] = useState('');
  const [dueDate, setDueDate] = useState('');
  const [templateId, setTemplateId] = useState('');
  const [lineItems, setLineItems] = useState([emptyLineItem()]);
  const [error, setError] = useState(null);
  const [saving, setSaving] = useState(false);

  const updateLineItem = (index, field, value) => {
    setLineItems((prev) => prev.map((li, i) => (i === index ? { ...li, [field]: value } : li)));
  };

  const handleSave = async () => {
    const dto = {
      invoiceNumber,
      clientName,
      issueDate: issueDate ? new Date(issueDate).toISOString() : '',
      dueDate: dueDate ? new Date(dueDate).toISOString() : '',
      notes: undefined,
      lineItems: lineItems.map((li) => ({ ...li, quantity: Number(li.quantity), unitPrice: Number(li.unitPrice) })),
    };

    const validationErrors = validateInvoice(dto);
    if (hasErrors(validationErrors)) {
      setError(Object.values(validationErrors)[0]);
      return;
    }

    setSaving(true);
    setError(null);
    try {
      await onSave(dto, templateId || null);
    } catch (err) {
      setError(err.message);
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="fixed inset-0 bg-gray-900/50 backdrop-blur-sm flex items-center justify-center p-4 z-50">
      <div className="bg-white rounded-2xl shadow-xl overflow-hidden w-full max-w-lg max-h-[90vh] flex flex-col">
        <div className="px-6 py-4 border-b border-gray-100 flex items-center justify-between">
          <h3 className="text-base font-semibold text-gray-900">{title}</h3>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600" aria-label="Close">
            <X className="h-4 w-4" />
          </button>
        </div>

        <div className="px-6 py-5 space-y-4 overflow-y-auto">
          {error && <div className="text-red-600 text-sm">{error}</div>}

          {templates && templates.length > 0 && (
            <div>
              <label className="block text-sm text-gray-600 mb-1">Start from a template (optional)</label>
              <select
                value={templateId}
                onChange={(e) => setTemplateId(e.target.value)}
                className="w-full border border-gray-300 rounded-[10px] px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow"
              >
                <option value="">None - blank invoice</option>
                {templates.map((t) => (
                  <option key={t.id} value={t.id}>{t.name} ({t.originalFileName})</option>
                ))}
              </select>
            </div>
          )}

          <div className="grid grid-cols-2 gap-3">
            <input placeholder="Invoice number" value={invoiceNumber} onChange={(e) => setInvoiceNumber(e.target.value)} className="border border-gray-300 rounded-[10px] px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow" />
            <input placeholder="Client name" value={clientName} onChange={(e) => setClientName(e.target.value)} className="border border-gray-300 rounded-[10px] px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow" />
            <input type="date" value={issueDate} onChange={(e) => setIssueDate(e.target.value)} className="border border-gray-300 rounded-[10px] px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow" />
            <input type="date" value={dueDate} onChange={(e) => setDueDate(e.target.value)} className="border border-gray-300 rounded-[10px] px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow" />
          </div>

          <div>
            <label className="block text-sm text-gray-600 mb-1">Line items</label>
            {lineItems.map((li, i) => (
              <div key={i} className="flex space-x-2 mb-2">
                <input
                  placeholder="Description"
                  value={li.description}
                  onChange={(e) => updateLineItem(i, 'description', e.target.value)}
                  className="flex-1 border border-gray-300 rounded-lg px-2 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow"
                />
                <input
                  type="number"
                  placeholder="Qty"
                  value={li.quantity}
                  onChange={(e) => updateLineItem(i, 'quantity', e.target.value)}
                  className="w-16 border border-gray-300 rounded-lg px-2 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow"
                />
                <input
                  type="number"
                  placeholder="Unit price"
                  value={li.unitPrice}
                  onChange={(e) => updateLineItem(i, 'unitPrice', e.target.value)}
                  className="w-24 border border-gray-300 rounded-lg px-2 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-shadow"
                />
              </div>
            ))}
            <button
              onClick={() => setLineItems((prev) => [...prev, emptyLineItem()])}
              className="text-sm text-blue-600 hover:text-blue-800"
            >
              + Add line item
            </button>
          </div>
        </div>

        <div className="px-6 py-4 border-t border-gray-100 flex justify-end gap-3">
          <button onClick={onClose} className="inline-flex items-center gap-2 bg-white text-gray-700 border border-gray-300 px-4 py-2.5 rounded-[10px] text-sm font-semibold hover:bg-gray-50 transition-colors">Cancel</button>
          <button
            onClick={handleSave}
            disabled={saving || !invoiceNumber.trim() || !clientName.trim()}
            className="inline-flex items-center gap-2 bg-blue-600 text-white px-4 py-2.5 rounded-[10px] text-sm font-semibold hover:bg-blue-700 shadow-sm transition-colors disabled:opacity-50"
          >
            {saving ? 'Saving...' : 'Create'}
          </button>
        </div>
      </div>
    </div>
  );
};

const InvoicesPage = ({ apiService, user }) => {
  const toast = useToast();
  const [invoices, setInvoices] = useState(null);
  const [templates, setTemplates] = useState([]);
  const [error, setError] = useState(null);
  const [selectedId, setSelectedId] = useState(null);
  const [showForm, setShowForm] = useState(false);
  const [showTemplates, setShowTemplates] = useState(false);

  const canView = hasPermission(user?.permissions, 'finance.view');
  const canManage = hasPermission(user?.permissions, 'finance.manage');

  const load = async () => {
    try {
      const [invoiceList, templateList] = await Promise.all([
        apiService.getInvoices(),
        apiService.getInvoiceTemplates(),
      ]);
      setInvoices(invoiceList || []);
      setTemplates(templateList || []);
    } catch (err) {
      setError(err.message);
    }
  };

  useEffect(() => {
    if (canView) load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [canView]);

  const selectedInvoice = invoices?.find((i) => i.id === selectedId);

  const handleCreate = async (dto, templateId) => {
    // Not try/caught - InvoiceFormModal's own onSave await/catch needs the rejection to show its
    // inline form error and keep the modal open with what the user typed, same contract as
    // WikiPageEditor/VaultEntryModal's onSave.
    const created = templateId
      ? await apiService.createInvoiceFromTemplate(templateId, dto)
      : await apiService.createInvoice(dto);
    setShowForm(false);
    await load();
    setSelectedId(created.id);
    toast.success('Invoice created.');
  };

  const refreshSelected = async () => {
    await load();
  };

  const handleDeleteTemplate = async (template) => {
    if (!window.confirm(`Delete template "${template.name}"?`)) return;
    try {
      await apiService.deleteInvoiceTemplate(template.id);
      await load();
      toast.success('Template deleted.');
    } catch (err) {
      reportApiError(toast, err, 'Could not delete this template.');
    }
  };

  if (!canView) {
    return (
      <div className="bg-white rounded-2xl border border-gray-100 shadow-sm p-8 text-center text-gray-400">
        You don't have access to Finance.
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div className="flex justify-between items-center">
        <div>
          <h2 className="text-[27px] font-bold text-gray-900 flex items-center">
            <RandIcon className="h-7 w-7 mr-2 text-gray-700" />
            Invoices
          </h2>
          <p className="text-gray-600">
            {invoices && invoices.length > 0
              ? (() => {
                  const outstanding = invoices
                    .filter((inv) => inv.status === 'Sent' || inv.status === 'Overdue')
                    .reduce((sum, inv) => sum + inv.total, 0);
                  const overdueCount = invoices.filter((inv) => inv.status === 'Overdue').length;
                  return `R${outstanding.toLocaleString(undefined, { maximumFractionDigits: 0 })} outstanding · ${overdueCount} overdue`;
                })()
              : 'Create invoices, attach source documents, and reuse layouts as templates'}
          </p>
        </div>
        {canManage && (
          <button
            onClick={() => setShowForm(true)}
            className="inline-flex items-center gap-2 bg-blue-600 text-white px-4 py-2.5 rounded-[10px] text-sm font-semibold hover:bg-blue-700 shadow-sm transition-colors"
          >
            <Plus className="h-5 w-5" />
            New Invoice
          </button>
        )}
      </div>

      {error && <div className="text-red-600 text-sm">{error}</div>}

      {invoices && invoices.length > 0 && (() => {
        const now = new Date();
        const paidThisMonth = invoices
          .filter((inv) => inv.status === 'Paid' && new Date(inv.issueDate).getMonth() === now.getMonth() && new Date(inv.issueDate).getFullYear() === now.getFullYear())
          .reduce((sum, inv) => sum + inv.total, 0);
        const awaitingPayment = invoices
          .filter((inv) => inv.status === 'Sent')
          .reduce((sum, inv) => sum + inv.total, 0);
        const overdue = invoices
          .filter((inv) => inv.status === 'Overdue')
          .reduce((sum, inv) => sum + inv.total, 0);
        const overdueCount = invoices.filter((inv) => inv.status === 'Overdue').length;
        const fmt = (n) => `R${n.toLocaleString(undefined, { minimumFractionDigits: 0, maximumFractionDigits: 0 })}`;

        const paidWithDates = invoices.filter((inv) => inv.status === 'Paid' && inv.paidAt);
        const avgDaysToPay = paidWithDates.length > 0
          ? Math.round(
              paidWithDates.reduce((sum, inv) => sum + (new Date(inv.paidAt) - new Date(inv.issueDate)) / 86400000, 0) /
                paidWithDates.length
            )
          : null;

        return (
          <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
            <div className="bg-white p-5 rounded-[14px] border border-gray-100 shadow-sm">
              <p className="text-sm font-medium text-gray-500">Paid this month</p>
              <p className="text-2xl font-bold text-gray-900">{fmt(paidThisMonth)}</p>
            </div>
            <div className="bg-white p-5 rounded-[14px] border border-gray-100 shadow-sm">
              <p className="text-sm font-medium text-gray-500">Awaiting payment</p>
              <p className="text-2xl font-bold text-gray-900">{fmt(awaitingPayment)}</p>
            </div>
            <div className={`bg-white p-5 rounded-[14px] border shadow-sm ${overdueCount > 0 ? 'border-[#DB4241]/30' : 'border-gray-100'}`}>
              <p className={`text-sm font-medium ${overdueCount > 0 ? 'text-red-600' : 'text-gray-500'}`}>Overdue</p>
              <p className="text-2xl font-bold text-gray-900">{fmt(overdue)}</p>
              {overdueCount > 0 && <p className="text-xs text-red-600 mt-1">{overdueCount} invoice{overdueCount !== 1 ? 's' : ''}</p>}
            </div>
            <div className="bg-white p-5 rounded-[14px] border border-gray-100 shadow-sm">
              <p className="text-sm font-medium text-gray-500">Avg. days to pay</p>
              <p className="text-2xl font-bold text-gray-900">{avgDaysToPay ?? '—'}</p>
            </div>
          </div>
        );
      })()}

      {templates.length > 0 && (
        <div className="bg-white rounded-2xl border border-gray-100 shadow-sm">
          <button
            onClick={() => setShowTemplates((v) => !v)}
            className="w-full text-sm text-gray-500 flex items-center px-4 py-3 hover:bg-gray-50/60 transition-colors"
          >
            <FileStack className="h-4 w-4 mr-1.5" />
            {templates.length} saved template{templates.length !== 1 ? 's' : ''} available when creating a new invoice
          </button>
          {showTemplates && (
            <div className="divide-y divide-gray-100 border-t border-gray-100">
              {templates.map((t) => (
                <div key={t.id} className="px-4 py-2.5 flex items-center justify-between text-sm">
                  <span className="text-gray-900">{t.name}{t.clientName && <span className="text-gray-400"> · {t.clientName}</span>}</span>
                  {canManage && (
                    <button onClick={() => handleDeleteTemplate(t)} className="text-red-400 hover:text-red-600 p-1" aria-label={`Delete template ${t.name}`}>
                      <X className="h-3.5 w-3.5" />
                    </button>
                  )}
                </div>
              ))}
            </div>
          )}
        </div>
      )}

      <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
        <div className="bg-white rounded-2xl border border-gray-100 shadow-sm divide-y divide-gray-100">
          {invoices === null && <div className="p-6 text-gray-400">Loading...</div>}
          {invoices?.length === 0 && <div className="p-6 text-center text-gray-400">No invoices yet.</div>}
          {invoices?.map((inv) => (
            <div
              key={inv.id}
              onClick={() => setSelectedId(inv.id)}
              className={`p-4 cursor-pointer hover:bg-gray-50/60 transition-colors flex justify-between items-center ${selectedId === inv.id ? 'bg-blue-50' : ''}`}
            >
              <div>
                <div className="font-mono font-medium text-gray-900">{inv.invoiceNumber}</div>
                <div className="text-sm text-gray-500 flex items-center gap-1.5 mt-0.5">
                  <span>{inv.clientName}</span>
                  <InvoiceStatusBadge status={inv.status} />
                </div>
              </div>
              <div className="text-sm font-semibold text-gray-900">{formatCurrency(inv.total)}</div>
            </div>
          ))}
        </div>

        <div>
          {selectedInvoice ? (
            <InvoiceDetail
              apiService={apiService}
              user={user}
              invoice={selectedInvoice}
              onClose={() => setSelectedId(null)}
              onChanged={refreshSelected}
              onDeleted={() => { setSelectedId(null); load(); }}
            />
          ) : (
            <div className="bg-white rounded-2xl border border-gray-100 shadow-sm p-8 text-center text-gray-400">
              Select an invoice to view details.
            </div>
          )}
        </div>
      </div>

      {showForm && (
        <InvoiceFormModal
          title="New Invoice"
          templates={templates}
          onSave={handleCreate}
          onClose={() => setShowForm(false)}
        />
      )}
    </div>
  );
};

export default InvoicesPage;
