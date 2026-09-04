import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, it, expect } from 'vitest';
import { server, API_BASE_URL } from '../../test/mswServer';
import { ToastProvider } from '../../contexts/ToastContext';
import { ConfirmProvider } from '../../contexts/ConfirmContext';
import ApiService from '../../services/ApiService';
import BroadcastEmail from './BroadcastEmail';

const roles = [
  { id: 1, name: 'Admin' },
  { id: 2, name: 'Manager' },
  { id: 3, name: 'Member' },
];

const renderComponent = () => {
  const apiService = new ApiService();
  return render(
    <ToastProvider>
      <ConfirmProvider>
        <BroadcastEmail apiService={apiService} />
      </ConfirmProvider>
    </ToastProvider>
  );
};

const fillAndSubmit = async (user, { role = 'Manager', subject = 'Heads up', body = 'New feature released.' } = {}) => {
  await user.click(screen.getByRole('checkbox', { name: role }));
  await user.type(screen.getByLabelText(/subject/i), subject);
  await user.type(screen.getByLabelText(/message/i), body);
  await user.click(screen.getByRole('button', { name: /^send$/i }));
};

describe('BroadcastEmail', () => {
  it('loads roles as checkboxes', async () => {
    server.use(http.get(`${API_BASE_URL}/roles`, () => HttpResponse.json(roles)));
    renderComponent();

    expect(await screen.findByRole('checkbox', { name: 'Admin' })).toBeInTheDocument();
    expect(screen.getByRole('checkbox', { name: 'Manager' })).toBeInTheDocument();
    expect(screen.getByRole('checkbox', { name: 'Member' })).toBeInTheDocument();
  });

  it('shows a confirmation dialog naming the selected roles, and sends only after confirming', async () => {
    server.use(
      http.get(`${API_BASE_URL}/roles`, () => HttpResponse.json(roles)),
      http.post(`${API_BASE_URL}/communications/broadcast`, () => HttpResponse.json({ recipientCount: 4 }))
    );
    const user = userEvent.setup();
    renderComponent();
    await screen.findByRole('checkbox', { name: 'Manager' });

    await fillAndSubmit(user);

    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByText(/Manager/)).toBeInTheDocument();
    await user.click(within(dialog).getByRole('button', { name: /^send$/i }));

    expect(await screen.findByText(/sent to 4 recipients/i)).toBeInTheDocument();
    // Form resets after a successful send.
    expect(screen.getByLabelText(/subject/i)).toHaveValue('');
  });

  it('does not send when the confirmation is cancelled', async () => {
    server.use(http.get(`${API_BASE_URL}/roles`, () => HttpResponse.json(roles)));
    const user = userEvent.setup();
    renderComponent();
    await screen.findByRole('checkbox', { name: 'Manager' });

    await fillAndSubmit(user);
    const dialog = await screen.findByRole('dialog');
    await user.click(within(dialog).getByRole('button', { name: /cancel/i }));

    // The subject the user typed is still there - nothing was sent or reset.
    expect(screen.getByLabelText(/subject/i)).toHaveValue('Heads up');
  });

  it('requires at least one role, a subject, and a body before sending', async () => {
    server.use(http.get(`${API_BASE_URL}/roles`, () => HttpResponse.json(roles)));
    const user = userEvent.setup();
    renderComponent();
    await screen.findByRole('checkbox', { name: 'Manager' });

    await user.click(screen.getByRole('button', { name: /^send$/i }));

    expect(await screen.findByText(/subject and body are both required/i)).toBeInTheDocument();
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('toasts the server-provided error message when sending fails', async () => {
    server.use(
      http.get(`${API_BASE_URL}/roles`, () => HttpResponse.json(roles)),
      http.post(`${API_BASE_URL}/communications/broadcast`, () => HttpResponse.json({ message: 'SMTP unavailable' }, { status: 500 }))
    );
    const user = userEvent.setup();
    renderComponent();
    await screen.findByRole('checkbox', { name: 'Manager' });

    await fillAndSubmit(user);
    const dialog = await screen.findByRole('dialog');
    await user.click(within(dialog).getByRole('button', { name: /^send$/i }));

    expect(await screen.findByText('SMTP unavailable')).toBeInTheDocument();
  });
});
