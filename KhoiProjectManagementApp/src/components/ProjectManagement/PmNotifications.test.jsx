import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';
import PmNotifications from './PmNotifications';

const note = (over) => ({ id: 1, message: 'Sam mentioned you on Login page', isRead: false, createdAt: '2026-11-10T08:00:00Z', ...over });

const makeApi = (list) => ({
  getNotifications: vi.fn().mockResolvedValue(list),
  markNotificationAsRead: vi.fn().mockResolvedValue(null),
});

describe('PmNotifications', () => {
  it('shows the unread count on the bell', async () => {
    render(<PmNotifications apiService={makeApi([note({ id: 1 }), note({ id: 2, isRead: true }), note({ id: 3 })])} />);
    expect(await screen.findByRole('button', { name: 'Notifications, 2 unread' })).toBeInTheDocument();
  });

  it('lists newest first and marks one read on click', async () => {
    const api = makeApi([note({ id: 1, message: 'older', createdAt: '2026-11-01T08:00:00Z' }), note({ id: 2, message: 'newer', createdAt: '2026-11-09T08:00:00Z' })]);
    const user = userEvent.setup();
    render(<PmNotifications apiService={api} />);
    await user.click(await screen.findByRole('button', { name: /notifications/i }));

    const buttons = within(screen.getByRole('region', { name: 'Notifications' })).getAllByRole('button');
    expect(buttons[0]).toHaveTextContent('newer');

    await user.click(buttons[0]);

    await waitFor(() => expect(api.markNotificationAsRead).toHaveBeenCalledWith(2));
    expect(screen.getByRole('button', { name: 'Notifications, 1 unread' })).toBeInTheDocument();
  });

  it('shows an empty state', async () => {
    const user = userEvent.setup();
    render(<PmNotifications apiService={makeApi([])} />);
    await user.click(await screen.findByRole('button', { name: 'Notifications' }));
    expect(screen.getByText(/all caught up/i)).toBeInTheDocument();
  });

  it('closes with Escape', async () => {
    const user = userEvent.setup();
    render(<PmNotifications apiService={makeApi([note({})])} />);
    await user.click(await screen.findByRole('button', { name: /notifications/i }));
    expect(screen.getByRole('region', { name: 'Notifications' })).toBeInTheDocument();

    await user.keyboard('{Escape}');

    expect(screen.queryByRole('region', { name: 'Notifications' })).not.toBeInTheDocument();
  });
});
