import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, it, expect, beforeEach } from 'vitest';
import { server, API_BASE_URL } from '../../test/mswServer';
import { ToastProvider } from '../../contexts/ToastContext';
import ApiService from '../../services/ApiService';
import AuditLog from './AuditLog';

const renderComponent = () => {
  const apiService = new ApiService();
  return render(
    <ToastProvider>
      <AuditLog apiService={apiService} />
    </ToastProvider>
  );
};

// SentEmailsView debounces its load by 300ms (see AuditLog.jsx) - findBy's default 1000ms timeout
// covers that, but a generous explicit timeout keeps this from being flaky under load.
const FIND_OPTS = { timeout: 3000 };

describe('AuditLog', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it('shows the Sent Emails tab by default, loading then rendering rows from the audit log', async () => {
    server.use(
      http.get(`${API_BASE_URL}/audit/emails`, () => HttpResponse.json([
        { id: 1, toEmail: 'person@example.com', subject: 'Welcome', emailType: 'Welcome', sentAt: '2026-08-20T10:00:00Z', status: 'Sent' },
        { id: 2, toEmail: 'other@example.com', subject: 'Reset password', emailType: 'PasswordReset', sentAt: '2026-08-21T10:00:00Z', status: 'Failed', errorMessage: 'SMTP timeout' },
      ]))
    );
    renderComponent();

    expect(screen.getByRole('heading', { name: /^audit$/i })).toBeInTheDocument();
    expect(screen.getByText(/loading/i)).toBeInTheDocument();

    expect(await screen.findByText('person@example.com', {}, FIND_OPTS)).toBeInTheDocument();
    expect(screen.getByText('other@example.com')).toBeInTheDocument();
    // Scoped to <span> (the status badge) - a bare text search also matches a status-filter <option>.
    expect(screen.getByText('Sent', { selector: 'span' })).toBeInTheDocument();
    // Scoped to <span> (the status badge) - a bare text search also matches a status-filter <option>.
    expect(screen.getByText('Failed', { selector: 'span' })).toBeInTheDocument();
  });

  it('shows the "no emails match" empty state when the log is empty', async () => {
    server.use(http.get(`${API_BASE_URL}/audit/emails`, () => HttpResponse.json([])));
    renderComponent();

    expect(await screen.findByText(/no emails match/i, {}, FIND_OPTS)).toBeInTheDocument();
  });

  it('toasts the server-provided error message when loading the email audit log fails', async () => {
    server.use(
      http.get(`${API_BASE_URL}/audit/emails`, () => HttpResponse.json({ message: 'Audit log unavailable' }, { status: 500 }))
    );
    renderComponent();

    expect(await screen.findByText('Audit log unavailable', {}, FIND_OPTS)).toBeInTheDocument();
  });

  it('switching to Error Logs loads dates then entries for the auto-selected date', async () => {
    server.use(
      http.get(`${API_BASE_URL}/audit/emails`, () => HttpResponse.json([])),
      http.get(`${API_BASE_URL}/audit/error-logs/dates`, () => HttpResponse.json(['2026-08-27', '2026-08-26'])),
      http.get(`${API_BASE_URL}/audit/error-logs`, ({ request }) => {
        const url = new URL(request.url);
        expect(url.searchParams.get('date')).toBe('2026-08-27');
        return HttpResponse.json([
          { timestamp: '2026-08-27T09:00:00Z', level: 'ERR', message: 'Database connection lost' },
        ]);
      })
    );

    const user = userEvent.setup();
    renderComponent();

    await user.click(screen.getByRole('button', { name: /error logs/i }));

    expect(await screen.findByText(/database connection lost/i)).toBeInTheDocument();
    expect(screen.getByText('[ERR]')).toBeInTheDocument();
  });

  it('shows "no log files found" when there are no error log dates', async () => {
    server.use(
      http.get(`${API_BASE_URL}/audit/emails`, () => HttpResponse.json([])),
      http.get(`${API_BASE_URL}/audit/error-logs/dates`, () => HttpResponse.json([]))
    );

    const user = userEvent.setup();
    renderComponent();

    await user.click(screen.getByRole('button', { name: /error logs/i }));

    expect(await screen.findByText(/no log files found/i)).toBeInTheDocument();
  });
});
