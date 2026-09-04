import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, it, expect } from 'vitest';
import { server, API_BASE_URL } from '../../test/mswServer';
import { ToastProvider } from '../../contexts/ToastContext';
import ApiService from '../../services/ApiService';
import SystemOverviewEmailSettings from './SystemOverviewEmailSettings';

const seededSettings = {
  enabled: true,
  dayOfWeek: 5, // Friday
  hour: 10,
  minute: 0,
  updatedAtUtc: '2024-01-01T00:00:00Z',
  updatedByUserName: null,
};

const renderComponent = () => {
  const apiService = new ApiService();
  return render(
    <ToastProvider>
      <SystemOverviewEmailSettings apiService={apiService} />
    </ToastProvider>
  );
};

describe('SystemOverviewEmailSettings', () => {
  it('loads and displays the current schedule', async () => {
    server.use(http.get(`${API_BASE_URL}/communications/system-overview-email-settings`, () => HttpResponse.json(seededSettings)));
    renderComponent();

    expect(await screen.findByDisplayValue('Friday')).toBeInTheDocument();
    expect(screen.getByLabelText(/time/i)).toHaveValue('10:00');
    expect(screen.getByText('Enabled')).toBeInTheDocument();
  });

  it('saves a changed day/time/toggle and shows the updated "last changed" line', async () => {
    server.use(
      http.get(`${API_BASE_URL}/communications/system-overview-email-settings`, () => HttpResponse.json(seededSettings)),
      http.put(`${API_BASE_URL}/communications/system-overview-email-settings`, async ({ request }) => {
        const body = await request.json();
        return HttpResponse.json({
          ...body,
          updatedAtUtc: '2026-09-04T09:00:00Z',
          updatedByUserName: 'Kolisa Mjobo',
        });
      })
    );
    const user = userEvent.setup();
    renderComponent();
    await screen.findByDisplayValue('Friday');

    await user.click(screen.getByRole('checkbox'));
    await user.selectOptions(screen.getByLabelText(/day/i), 'Monday');
    fireEvent.change(screen.getByLabelText(/time/i), { target: { value: '09:15' } });
    await user.click(screen.getByRole('button', { name: /^save$/i }));

    await waitFor(() => expect(screen.getByText(/schedule saved/i)).toBeInTheDocument());
    expect(await screen.findByText(/last changed by kolisa mjobo/i)).toBeInTheDocument();
  });

  it('toasts the server-provided error message when saving fails', async () => {
    server.use(
      http.get(`${API_BASE_URL}/communications/system-overview-email-settings`, () => HttpResponse.json(seededSettings)),
      http.put(`${API_BASE_URL}/communications/system-overview-email-settings`, () =>
        HttpResponse.json({ message: 'Scheduler unavailable' }, { status: 500 }))
    );
    const user = userEvent.setup();
    renderComponent();
    await screen.findByDisplayValue('Friday');

    await user.click(screen.getByRole('button', { name: /^save$/i }));

    expect(await screen.findByText('Scheduler unavailable')).toBeInTheDocument();
  });
});
