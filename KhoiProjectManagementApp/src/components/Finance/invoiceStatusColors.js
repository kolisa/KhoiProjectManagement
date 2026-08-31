// Shared between InvoicesPage.jsx and InvoiceDetail.jsx - was previously duplicated as two identical
// hand-rolled InvoiceStatusBadge components; centralized so both drive the same colors through the
// shared Common/StatusBadge component instead.
export const INVOICE_STATUS_COLORS = {
  Paid: 'bg-[#E3F8E9] text-[#005F2E]',
  Sent: 'bg-[#FFEED6] text-[#874400]',
  Overdue: 'bg-[#FFEBE8] text-[#B71824]',
  // Draft deliberately omitted - StatusBadge's own neutral default already matches.
};
