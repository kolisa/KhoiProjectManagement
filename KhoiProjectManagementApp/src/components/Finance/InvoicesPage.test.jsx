import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { server, API_BASE_URL } from '../../test/mswServer';
import { ToastProvider } from '../../contexts/ToastContext';
import { ConfirmProvider } from '../../contexts/ConfirmContext';
import { formatCurrency } from '../../utils/currency';
import ApiService from '../../services/ApiService';
import InvoicesPage from './InvoicesPage';

const managerUser = { id: 2, name: 'Manager', permissions: ['finance.view', 'finance.manage'] };
const noAccessUser = { id: 3, name: 'Outsider', permissions: [] };

const renderInvoicesPage = (user = managerUser) => {
  const apiService = new ApiService();
  render(
    <ToastProvider>
      <ConfirmProvider>
        <InvoicesPage apiService={apiService} user={user} />
      </ConfirmProvider>
    </ToastProvider>
  );
  return apiService;
};

const makeInvoice = (overrides = {}) => ({
  id: 1,
  invoiceNumber: 'INV-1001',
  clientName: 'Acme Corp',
  status: 'Sent',
  issueDate: '2026-08-01T00:00:00',
  dueDate: '2026-08-15T00:00:00',
  paidAt: null,
  total: 1500,
  lineItems: [{ id: 1, description: 'Consulting', quantity: 10, unitPrice: 150 }],
  originalFileName: null,
  ...overrides,
});

// Every load() call fetches invoices and templates concurrently (Promise.all) - a test that grants
// finance.view but forgets this handler makes MSW's onUnhandledRequest: 'error' fail the test with a
// confusing "unhandled request" error instead of the actual assertion failure.
const mockTemplates = (templates = []) =>
  http.get(`${API_BASE_URL}/invoices/templates`, () => HttpResponse.json(templates));

// formatCurrency's toLocaleString(undefined, ...) call groups thousands with a non-breaking space in
// this environment's default locale - testing-library's getByText normalizer collapses that (and any
// other whitespace) in the DOM text it searches, but does NOT normalize the plain-string query you
// pass it, so comparing directly against formatCurrency(...)'s raw output never matches. Collapse it
// the same way before querying.
const currencyText = (amount, opts) => formatCurrency(amount, opts).replace(/\s+/g, ' ');

describe('InvoicesPage', () => {
  it('shows a loading state, then the invoice list with correctly formatted amounts', async () => {
    server.use(
      http.get(`${API_BASE_URL}/invoices`, () => HttpResponse.json([makeInvoice()])),
      mockTemplates()
    );

    renderInvoicesPage();

    expect(screen.getByText(/loading/i)).toBeInTheDocument();

    expect(await screen.findByText('INV-1001')).toBeInTheDocument();
    expect(screen.getByText('Acme Corp')).toBeInTheDocument();
    expect(screen.getByText('Sent')).toBeInTheDocument();
    // formatCurrency (R, 2 decimals, locale-grouped) - built from the real util rather than a
    // hardcoded string so the assertion doesn't drift from currency.js's actual formatting rules.
    expect(screen.getByText(currencyText(1500))).toBeInTheDocument();
  });

  it('shows an empty state when the API returns no invoices', async () => {
    server.use(
      http.get(`${API_BASE_URL}/invoices`, () => HttpResponse.json([])),
      mockTemplates()
    );

    renderInvoicesPage();

    expect(await screen.findByText(/no invoices yet/i)).toBeInTheDocument();
  });

  it('shows the server-provided error message when the invoices request fails', async () => {
    server.use(
      http.get(`${API_BASE_URL}/invoices`, () => HttpResponse.json({ message: 'boom' }, { status: 500 })),
      mockTemplates()
    );

    renderInvoicesPage();

    expect(await screen.findByText('boom')).toBeInTheDocument();
  });

  it("shows an access-denied message instead of loading data when the user lacks finance.view", () => {
    // No MSW handlers registered on purpose - load() must never fire for a user without finance.view,
    // and setup.js's onUnhandledRequest: 'error' would fail the test if it did.
    renderInvoicesPage(noAccessUser);

    expect(screen.getByText(/don't have access to finance/i)).toBeInTheDocument();
  });

  it('marks an invoice as Paid via the status dropdown, sends the right PUT body, and updates the badge', async () => {
    const invoice = makeInvoice({ status: 'Sent' });
    let putBody = null;
    server.use(
      http.get(`${API_BASE_URL}/invoices`, () => HttpResponse.json([invoice])),
      mockTemplates(),
      http.put(`${API_BASE_URL}/invoices/${invoice.id}/status`, async ({ request }) => {
        putBody = await request.json();
        invoice.status = 'Paid';
        invoice.paidAt = '2026-08-20T00:00:00';
        return HttpResponse.json(invoice);
      })
    );

    const user = userEvent.setup();
    renderInvoicesPage(managerUser);

    await user.click(await screen.findByText('INV-1001'));
    // Only one <select> exists here (no templates in this test), the InvoiceDetail status dropdown.
    const statusSelect = await screen.findByRole('combobox');
    await user.selectOptions(statusSelect, 'Paid');

    await waitFor(() => expect(putBody).toEqual({ status: 'Paid' }));

    // Both the list-row badge and the detail-pane badge flip to "Paid" once load() refetches. Scoped
    // to <span> (the badge markup) rather than a bare text search - the status <select> always has a
    // "Sent" <option> present regardless of which one is selected, which a plain queryByText('Sent')
    // would also match.
    await waitFor(() => expect(screen.getAllByText('Paid').length).toBeGreaterThanOrEqual(2));
    expect(screen.queryByText('Sent', { selector: 'span' })).not.toBeInTheDocument();
    expect(await screen.findByText('Marked as Paid.')).toBeInTheDocument();
  });

  it('creates an invoice from entered line items and shows a total matching what was typed', async () => {
    let postBody = null;
    let invoicesList = [];
    server.use(
      http.get(`${API_BASE_URL}/invoices`, () => HttpResponse.json(invoicesList)),
      mockTemplates(),
      http.post(`${API_BASE_URL}/invoices`, async ({ request }) => {
        postBody = await request.json();
        const total = postBody.lineItems.reduce((sum, li) => sum + li.quantity * li.unitPrice, 0);
        const created = {
          id: 42,
          invoiceNumber: postBody.invoiceNumber,
          clientName: postBody.clientName,
          status: 'Draft',
          issueDate: postBody.issueDate || null,
          dueDate: postBody.dueDate || null,
          paidAt: null,
          total,
          lineItems: postBody.lineItems.map((li, i) => ({ ...li, id: i + 1 })),
          originalFileName: null,
        };
        invoicesList = [...invoicesList, created];
        return HttpResponse.json(created);
      })
    );

    const user = userEvent.setup();
    renderInvoicesPage(managerUser);
    await screen.findByText(/no invoices yet/i);

    await user.click(screen.getByRole('button', { name: /new invoice/i }));
    await screen.findByRole('heading', { name: /new invoice/i });

    await user.type(screen.getByPlaceholderText('Invoice number'), 'INV-2002');
    await user.type(screen.getByPlaceholderText('Client name'), 'Globex Inc');

    const [description1] = screen.getAllByPlaceholderText('Description');
    await user.type(description1, 'Consulting');
    const [qty1] = screen.getAllByPlaceholderText('Qty');
    await user.clear(qty1);
    await user.type(qty1, '2');
    const [price1] = screen.getAllByPlaceholderText('Unit price');
    await user.clear(price1);
    await user.type(price1, '500');

    await user.click(screen.getByRole('button', { name: /add line item/i }));
    const description2 = screen.getAllByPlaceholderText('Description')[1];
    await user.type(description2, 'Design');
    const qty2 = screen.getAllByPlaceholderText('Qty')[1];
    await user.clear(qty2);
    await user.type(qty2, '1');
    const price2 = screen.getAllByPlaceholderText('Unit price')[1];
    await user.clear(price2);
    await user.type(price2, '250');

    await user.click(screen.getByRole('button', { name: /^create$/i }));

    await waitFor(() => expect(postBody).not.toBeNull());
    expect(postBody.lineItems).toEqual([
      { description: 'Consulting', quantity: 2, unitPrice: 500 },
      { description: 'Design', quantity: 1, unitPrice: 250 },
    ]);

    // 2 * 500 + 1 * 250 = 1250 - the total shown in the refreshed list must match what was typed.
    // The created invoice is auto-selected into the detail pane too, so both "INV-2002" and its total
    // legitimately appear twice (list row + detail header/total) - assert presence via count rather
    // than a single-match query.
    await waitFor(() => expect(screen.getAllByText('INV-2002').length).toBeGreaterThanOrEqual(1));
    expect(screen.getAllByText(currencyText(1250)).length).toBeGreaterThanOrEqual(1);
    expect(screen.getByText('Invoice created.')).toBeInTheDocument();
  });
});
