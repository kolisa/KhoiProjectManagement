import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, it, expect, beforeEach } from 'vitest';
import { server, API_BASE_URL } from '../../test/mswServer';
import { ToastProvider } from '../../contexts/ToastContext';
import ApiService from '../../services/ApiService';
import DashboardWidgetSettings from './DashboardWidgetSettings';

const prefs = [
  { widgetKey: 'tasks', displayName: 'My Tasks', isVisible: true, sortOrder: 0 },
  { widgetKey: 'notifications', displayName: 'Notifications', isVisible: false, sortOrder: 1 },
];

const catalog = [
  { widgetKey: 'tasks', displayName: 'My Tasks', description: 'Your assigned tasks', isEnabled: true },
  { widgetKey: 'notifications', displayName: 'Notifications', description: 'Recent notifications', isEnabled: false },
];

const nonAdminUser = { id: 1, permissions: ['dashboard.view'] };
const adminUser = { id: 2, permissions: ['dashboard.view', 'dashboard.manage'] };

const renderComponent = (user = nonAdminUser) => {
  const apiService = new ApiService();
  return render(
    <ToastProvider>
      <DashboardWidgetSettings apiService={apiService} user={user} />
    </ToastProvider>
  );
};

const mockPrefsOnly = () => {
  server.use(http.get(`${API_BASE_URL}/dashboard/widgets/my-preferences`, () => HttpResponse.json(prefs)));
};

describe('DashboardWidgetSettings', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it('shows a loading state before preferences arrive', () => {
    mockPrefsOnly();
    renderComponent();

    expect(screen.getByText(/loading dashboard settings/i)).toBeInTheDocument();
  });

  it('renders each widget with its visibility, and does not show the admin section for a non-admin user', async () => {
    mockPrefsOnly();
    renderComponent(nonAdminUser);

    expect(await screen.findByText('My Tasks')).toBeInTheDocument();
    expect(screen.getByText('Notifications')).toBeInTheDocument();
    expect(screen.queryByText(/widget availability/i)).not.toBeInTheDocument();
    // Only one "Notifications" - the personal-preferences one - since catalog was never fetched.
    expect(screen.getAllByText('Notifications')).toHaveLength(1);
  });

  it('toggling a widget\'s visibility PUTs the full preference list with the flipped flag', async () => {
    mockPrefsOnly();
    let putBody = null;
    server.use(
      http.put(`${API_BASE_URL}/dashboard/widgets/my-preferences`, async ({ request }) => {
        putBody = await request.json();
        return HttpResponse.json({});
      })
    );

    const user = userEvent.setup();
    renderComponent(nonAdminUser);
    await screen.findByText('My Tasks');

    const notificationsRow = screen.getByText('Notifications').closest('div.p-3');
    await user.click(within(notificationsRow).getByRole('checkbox'));

    await waitFor(() => expect(putBody).not.toBeNull());
    expect(putBody).toEqual([
      { widgetKey: 'tasks', isVisible: true, sortOrder: 0 },
      { widgetKey: 'notifications', isVisible: true, sortOrder: 1 },
    ]);
  });

  it('moving a widget down reorders the list and PUTs the new sortOrder values', async () => {
    mockPrefsOnly();
    let putBody = null;
    server.use(
      http.put(`${API_BASE_URL}/dashboard/widgets/my-preferences`, async ({ request }) => {
        putBody = await request.json();
        return HttpResponse.json({});
      })
    );

    const user = userEvent.setup();
    renderComponent(nonAdminUser);
    await screen.findByText('My Tasks');

    const tasksRow = screen.getByText('My Tasks').closest('div.p-3');
    // First row's "Move up" is disabled (nothing above it) - a real boundary check, not padding.
    expect(within(tasksRow).getByRole('button', { name: /move up/i })).toBeDisabled();
    await user.click(within(tasksRow).getByRole('button', { name: /move down/i }));

    await waitFor(() => expect(putBody).not.toBeNull());
    expect(putBody).toEqual([
      { widgetKey: 'notifications', isVisible: false, sortOrder: 0 },
      { widgetKey: 'tasks', isVisible: true, sortOrder: 1 },
    ]);
  });

  it('renders an inline error (not a toast) when the initial load fails', async () => {
    server.use(
      http.get(`${API_BASE_URL}/dashboard/widgets/my-preferences`, () => HttpResponse.json({ message: 'Widgets unavailable' }, { status: 500 }))
    );
    renderComponent(nonAdminUser);

    expect(await screen.findByText(/widgets unavailable/i)).toBeInTheDocument();
  });

  it('shows the admin allowlist section for a user with dashboard.manage, and toggling an entry PUTs the allowlist', async () => {
    mockPrefsOnly();
    server.use(http.get(`${API_BASE_URL}/dashboard/widgets/catalog`, () => HttpResponse.json(catalog)));
    let putBody = null;
    server.use(
      http.put(`${API_BASE_URL}/dashboard/widgets/allowlist`, async ({ request }) => {
        putBody = await request.json();
        return HttpResponse.json({});
      })
    );

    const user = userEvent.setup();
    renderComponent(adminUser);

    expect(await screen.findByRole('heading', { name: /widget availability/i })).toBeInTheDocument();
    // Now "Notifications" appears twice: once in the personal list, once in the admin catalog.
    const [, adminNotificationsText] = await screen.findAllByText('Notifications');
    const adminRow = adminNotificationsText.closest('div.p-3');
    await user.click(within(adminRow).getByRole('checkbox'));

    await waitFor(() => expect(putBody).not.toBeNull());
    expect(putBody).toEqual([{ widgetKey: 'notifications', isEnabled: true }]);
  });
});
