import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { beforeEach, describe, expect, it } from 'vitest';
import { server, API_BASE_URL } from '../../test/mswServer';
import { ToastProvider } from '../../contexts/ToastContext';
import ApiService from '../../services/ApiService';
import RemindersPage from './RemindersPage';

// RemindersPage reads/writes its filters via window.location.search + history.replaceState (no
// router library - see CLAUDE.md's frontend-structure note), so every test needs a clean URL or
// filter state leaks between tests through the shared jsdom `window`.
beforeEach(() => {
  window.history.replaceState(null, '', '/');
  // Always called on mount to populate the (optional) filter/assignee dropdowns - stub with empty
  // lists by default so individual tests don't have to care about them unless they're the focus.
  server.use(
    http.get(`${API_BASE_URL}/users`, () => HttpResponse.json([])),
    http.get(`${API_BASE_URL}/projects`, () => HttpResponse.json([]))
  );
});

const testUser = {
  id: 1,
  name: 'Test Admin',
  email: 'admin@khoitech.africa',
  permissions: ['reminders.view_all', 'reminders.manage'],
};

const summaryFixture = {
  totalActive: 3,
  dueToday: 1,
  upcoming: 1,
  overdue: 1,
  completed: 0,
  highPriority: 1,
};

const reminderFixture = (overrides = {}) => ({
  id: 1,
  title: 'Renew SSL certificate',
  category: 'Ops',
  dueAt: new Date(Date.now() + 3600_000).toISOString(),
  priority: 'high',
  status: 'Pending',
  assignedToName: 'Test Admin',
  isOverdue: false,
  ...overrides,
});

const renderPage = (user = testUser) => {
  const apiService = new ApiService();
  render(
    <ToastProvider>
      <RemindersPage apiService={apiService} user={user} />
    </ToastProvider>
  );
};

// ReminderList renders every row twice in the DOM at once - a desktop <table> (hidden via a
// `md:table`/CSS class jsdom doesn't evaluate, not by unmounting) and a `md:hidden` mobile card
// list with duplicate titles/buttons carrying the same accessible names. The <table> is real
// semantic markup, so scoping queries to it (rather than to `screen` directly) is what keeps
// `getByText`/`getByRole` queries below from throwing on "multiple elements found".
const findReminderTable = async () => within(await screen.findByRole('table'));

describe('RemindersPage - loading, list and summary', () => {
  it('shows a loading state before the reminders request resolves', async () => {
    server.use(
      http.get(`${API_BASE_URL}/reminders`, async () => {
        await new Promise((resolve) => setTimeout(resolve, 30));
        return HttpResponse.json([reminderFixture()]);
      }),
      http.get(`${API_BASE_URL}/reminders/summary`, () => HttpResponse.json(summaryFixture))
    );

    renderPage();

    expect(screen.getByLabelText(/loading reminders/i)).toBeInTheDocument();
    await waitFor(() => expect(screen.queryByLabelText(/loading reminders/i)).not.toBeInTheDocument());
  });

  it('renders the returned reminders and summary card counts', async () => {
    server.use(
      http.get(`${API_BASE_URL}/reminders`, () => HttpResponse.json([
        reminderFixture(),
        reminderFixture({ id: 2, title: 'File Q2 report', priority: 'medium', status: 'Snoozed' }),
      ])),
      http.get(`${API_BASE_URL}/reminders/summary`, () => HttpResponse.json(summaryFixture))
    );

    renderPage();

    const table = await findReminderTable();
    expect(table.getByText('Renew SSL certificate')).toBeInTheDocument();
    expect(table.getByText('File Q2 report')).toBeInTheDocument();

    // Summary cards render the counts straight from the summary payload.
    expect(screen.getByText('Due Today').closest('button')).toHaveTextContent('1');
    // Scoped to the summary card's label <div> - a bare text search also matches the "Overdue" view
    // tab button elsewhere on the page.
    expect(screen.getByText('Overdue', { selector: 'div' }).closest('button')).toHaveTextContent('1');
    expect(screen.getByText('High Priority').closest('button')).toHaveTextContent('1');
  });

  it('shows the empty state and a "create your first reminder" link when there are no reminders and no filters', async () => {
    server.use(
      http.get(`${API_BASE_URL}/reminders`, () => HttpResponse.json([])),
      http.get(`${API_BASE_URL}/reminders/summary`, () => HttpResponse.json(summaryFixture))
    );

    renderPage();

    expect(await screen.findByText(/no reminders yet/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /create your first reminder/i })).toBeInTheDocument();
  });

  it('shows an error message without crashing the page when the reminders request fails', async () => {
    server.use(
      http.get(`${API_BASE_URL}/reminders`, () => HttpResponse.json({ message: 'Database unavailable' }, { status: 500 })),
      http.get(`${API_BASE_URL}/reminders/summary`, () => HttpResponse.json(summaryFixture))
    );

    renderPage();

    expect(await screen.findByText(/database unavailable/i)).toBeInTheDocument();
    // The rest of the shell (heading, "New Reminder" button) must still be intact - a crash would
    // take it down too, and the summary cards (a separate, non-throwing fetch) should still render.
    expect(screen.getByRole('heading', { name: /my reminders/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /new reminder/i })).toBeInTheDocument();
    expect(await screen.findByText('Due Today')).toBeInTheDocument();
  });
});

describe('RemindersPage - view tab filtering', () => {
  it('re-fetches with view=overdue when the Overdue tab is clicked', async () => {
    const seenQueries = [];
    server.use(
      http.get(`${API_BASE_URL}/reminders`, ({ request }) => {
        seenQueries.push(new URL(request.url).searchParams.get('view'));
        return HttpResponse.json([reminderFixture()]);
      }),
      http.get(`${API_BASE_URL}/reminders/summary`, () => HttpResponse.json(summaryFixture))
    );
    const user = userEvent.setup();
    renderPage();
    await findReminderTable();
    expect(seenQueries).toEqual([null]); // initial load, no filter

    await user.click(screen.getByRole('button', { name: /^overdue$/i }));

    await waitFor(() => expect(seenQueries).toEqual([null, 'overdue']));
    // The filter is also reflected back into the URL so it survives a refresh/share.
    expect(new URLSearchParams(window.location.search).get('view')).toBe('overdue');
  });

  it('re-fetches with view=today when the Today tab is clicked, and highlights it as active', async () => {
    const seenQueries = [];
    server.use(
      http.get(`${API_BASE_URL}/reminders`, ({ request }) => {
        seenQueries.push(new URL(request.url).searchParams.get('view'));
        return HttpResponse.json([reminderFixture()]);
      }),
      http.get(`${API_BASE_URL}/reminders/summary`, () => HttpResponse.json(summaryFixture))
    );
    const user = userEvent.setup();
    renderPage();
    await findReminderTable();

    const todayTab = screen.getByRole('button', { name: /^today$/i });
    await user.click(todayTab);

    await waitFor(() => expect(seenQueries).toEqual([null, 'today']));
    expect(todayTab.className).toMatch(/border-blue-600/);
  });

  it('clicking the Upcoming tab after Overdue applies the new filter (not both at once)', async () => {
    const seenQueries = [];
    server.use(
      http.get(`${API_BASE_URL}/reminders`, ({ request }) => {
        seenQueries.push(new URL(request.url).searchParams.get('view'));
        return HttpResponse.json([reminderFixture()]);
      }),
      http.get(`${API_BASE_URL}/reminders/summary`, () => HttpResponse.json(summaryFixture))
    );
    const user = userEvent.setup();
    renderPage();
    await findReminderTable();

    await user.click(screen.getByRole('button', { name: /^overdue$/i }));
    await waitFor(() => expect(seenQueries).toEqual([null, 'overdue']));

    await user.click(screen.getByRole('button', { name: /^upcoming$/i }));
    await waitFor(() => expect(seenQueries).toEqual([null, 'overdue', 'upcoming']));
  });
});

describe('RemindersPage - complete and snooze actions', () => {
  it('completing a reminder POSTs to /reminders/:id/complete and refreshes the list', async () => {
    let completeCallCount = 0;
    server.use(
      http.get(`${API_BASE_URL}/reminders`, () => HttpResponse.json(
        completeCallCount === 0
          ? [reminderFixture()]
          : [reminderFixture({ status: 'Completed' })]
      )),
      http.get(`${API_BASE_URL}/reminders/summary`, () => HttpResponse.json(summaryFixture)),
      http.post(`${API_BASE_URL}/reminders/1/complete`, () => {
        completeCallCount += 1;
        return HttpResponse.json({ ok: true });
      })
    );
    const user = userEvent.setup();
    renderPage();
    let table = await findReminderTable();

    await user.click(table.getByRole('button', { name: /mark renew ssl certificate complete/i }));

    await waitFor(() => expect(completeCallCount).toBe(1));
    // After the refresh, the row now shows the "Reopen" action instead of "Complete". Re-scope to
    // the table (rather than a stale `table` reference or a bare `screen` query, which would match
    // the mobile card's duplicate button too) since the row re-renders with the refreshed data.
    table = within(screen.getByRole('table'));
    expect(await table.findByRole('button', { name: /reopen renew ssl certificate/i })).toBeInTheDocument();
  });

  it('surfaces an error toast and does not refresh the list when completing a reminder fails', async () => {
    server.use(
      http.get(`${API_BASE_URL}/reminders`, () => HttpResponse.json([reminderFixture()])),
      http.get(`${API_BASE_URL}/reminders/summary`, () => HttpResponse.json(summaryFixture)),
      http.post(`${API_BASE_URL}/reminders/1/complete`, () => HttpResponse.json({ message: 'Reminder locked' }, { status: 409 }))
    );
    const user = userEvent.setup();
    renderPage();
    const table = await findReminderTable();

    await user.click(table.getByRole('button', { name: /mark renew ssl certificate complete/i }));

    expect(await screen.findByText('Reminder locked')).toBeInTheDocument();
    // Still shows the "Complete" action - the failed request never actually completed it.
    expect(table.getByRole('button', { name: /mark renew ssl certificate complete/i })).toBeInTheDocument();
  });

  it('snoozing a reminder POSTs the chosen snoozeUntil and refreshes the list', async () => {
    let snoozeBody = null;
    server.use(
      http.get(`${API_BASE_URL}/reminders`, () => HttpResponse.json(
        snoozeBody === null
          ? [reminderFixture()]
          : [reminderFixture({ status: 'Snoozed' })]
      )),
      http.get(`${API_BASE_URL}/reminders/summary`, () => HttpResponse.json(summaryFixture)),
      http.post(`${API_BASE_URL}/reminders/1/snooze`, async ({ request }) => {
        snoozeBody = await request.json();
        return HttpResponse.json({ ok: true });
      })
    );
    const user = userEvent.setup();
    renderPage();
    const table = await findReminderTable();

    await user.click(table.getByRole('button', { name: /more actions for renew ssl certificate/i }));
    await user.click(table.getByRole('menuitem', { name: /tomorrow/i }));

    await waitFor(() => expect(snoozeBody).not.toBeNull());
    expect(snoozeBody).toHaveProperty('snoozeUntil');
    expect(new Date(snoozeBody.snoozeUntil).getTime()).toBeGreaterThan(Date.now());
    expect(await table.findByText('Snoozed')).toBeInTheDocument();
  });
});
