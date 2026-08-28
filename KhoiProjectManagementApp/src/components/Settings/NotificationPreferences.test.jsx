import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, it, expect, beforeEach } from 'vitest';
import { server, API_BASE_URL } from '../../test/mswServer';
import { ToastProvider } from '../../contexts/ToastContext';
import ApiService from '../../services/ApiService';
import NotificationPreferences from './NotificationPreferences';

const preferences = [
  { notificationType: 'TaskAssigned', displayName: 'Task Assigned', description: 'When a task is assigned to you', emailEnabled: false },
  { notificationType: 'ProjectUpdated', displayName: 'Project Updated', description: 'When a project changes', emailEnabled: true },
];

const renderComponent = () => {
  const apiService = new ApiService();
  return render(
    <ToastProvider>
      <NotificationPreferences apiService={apiService} />
    </ToastProvider>
  );
};

// The Toggle component (Common/Toggle.jsx) renders a bare, unlabeled checkbox - scope to the row
// by its displayName text to reliably grab the right one.
const checkboxForRow = (displayName) => {
  const row = screen.getByText(displayName).closest('div.p-4');
  return row.querySelector('input[type="checkbox"]');
};

describe('NotificationPreferences', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it('shows a loading state before preferences arrive', () => {
    server.use(http.get(`${API_BASE_URL}/notifications/preferences`, () => HttpResponse.json(preferences)));
    renderComponent();

    expect(screen.getByText(/loading preferences/i)).toBeInTheDocument();
  });

  it('renders each preference with its current emailEnabled state', async () => {
    server.use(http.get(`${API_BASE_URL}/notifications/preferences`, () => HttpResponse.json(preferences)));
    renderComponent();

    expect(await screen.findByText('Task Assigned')).toBeInTheDocument();
    expect(screen.getByText('Project Updated')).toBeInTheDocument();
    expect(checkboxForRow('Task Assigned')).not.toBeChecked();
    expect(checkboxForRow('Project Updated')).toBeChecked();
  });

  it('toggling a preference PUTs only that notificationType with the new emailEnabled value', async () => {
    server.use(http.get(`${API_BASE_URL}/notifications/preferences`, () => HttpResponse.json(preferences)));
    let putBody = null;
    server.use(
      http.put(`${API_BASE_URL}/notifications/preferences`, async ({ request }) => {
        putBody = await request.json();
        return HttpResponse.json({});
      })
    );

    const user = userEvent.setup();
    renderComponent();
    await screen.findByText('Task Assigned');

    await user.click(checkboxForRow('Task Assigned'));

    await waitFor(() => expect(putBody).not.toBeNull());
    expect(putBody).toEqual([{ notificationType: 'TaskAssigned', emailEnabled: true }]);
    expect(checkboxForRow('Task Assigned')).toBeChecked();
    // The untouched preference is left alone.
    expect(checkboxForRow('Project Updated')).toBeChecked();
  });

  it('renders an inline error (not a toast) when the initial load fails', async () => {
    server.use(
      http.get(`${API_BASE_URL}/notifications/preferences`, () => HttpResponse.json({ message: 'Preferences unavailable' }, { status: 500 }))
    );
    renderComponent();

    expect(await screen.findByText(/error: preferences unavailable/i)).toBeInTheDocument();
    expect(screen.queryByText(/loading preferences/i)).not.toBeInTheDocument();
  });

  it('reverts the toggle and toasts an error when saving a preference fails', async () => {
    server.use(
      http.get(`${API_BASE_URL}/notifications/preferences`, () => HttpResponse.json(preferences)),
      http.put(`${API_BASE_URL}/notifications/preferences`, () =>
        HttpResponse.json({ message: 'Could not save that preference' }, { status: 500 }))
    );

    const user = userEvent.setup();
    renderComponent();
    await screen.findByText('Task Assigned');

    const checkbox = checkboxForRow('Task Assigned');
    await user.click(checkbox);

    // The optimistic "checked" state is deliberately transient here - the mocked PUT rejects near-
    // instantly, so that state and its revert can both flush within the same microtask window,
    // making a synchronous "checked immediately after click" assertion racy. What's behaviorally
    // guaranteed and worth verifying is the end state: reverted (never lying about what's actually
    // saved) with the failure surfaced.
    await waitFor(() => expect(checkboxForRow('Task Assigned')).not.toBeChecked());
    expect(await screen.findByText('Could not save that preference')).toBeInTheDocument();
  });
});
