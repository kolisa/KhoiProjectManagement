import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';
import { ToastProvider } from '../../contexts/ToastContext';
import PmReports from './PmReports';

const statuses = ['completed', 'in-progress', 'blocked', 'todo'];
const day = (offset, counts) => ({ date: `2026-11-0${offset}T00:00:00Z`, counts });

const makeApi = (overrides = {}) => ({
  getCumulativeFlow: vi.fn().mockResolvedValue({
    statuses,
    points: [day(1, { completed: 0, 'in-progress': 1, blocked: 0, todo: 3 }), day(2, { completed: 1, 'in-progress': 1, blocked: 1, todo: 2 })],
  }),
  getFlowMetrics: vi.fn().mockResolvedValue({
    days: 30, completedCount: 4, completedPoints: 13,
    leadTime: { samples: 4, average: 6.5, median: 5, p85: 9 },
    cycleTime: { samples: 3, average: 3.2, median: 3, p85: 4.5 },
    weeks: [{ weekStart: '2026-11-02T00:00:00Z', items: 4, points: 13 }],
  }),
  getWorkloadReport: vi.fn().mockResolvedValue([
    { userId: 1, name: 'Dana', openItems: 3, openPoints: 8, inProgress: 1, blocked: 1, overdue: 2, doneLast30Days: 5 },
    { userId: null, name: 'Unassigned', openItems: 1, openPoints: 2, inProgress: 0, blocked: 0, overdue: 0, doneLast30Days: 0 },
  ]),
  exportWorkItemsCsv: vi.fn().mockResolvedValue(undefined),
  ...overrides,
});

const renderReports = (api) =>
  render(<ToastProvider><PmReports apiService={api} project={{ id: 5 }} /></ToastProvider>);

describe('PmReports', () => {
  it('renders flow, lead/cycle time and workload from the API', async () => {
    const api = makeApi();
    renderReports(api);

    expect(await screen.findByText('Cumulative flow')).toBeInTheDocument();
    expect(api.getCumulativeFlow).toHaveBeenCalledWith(5, 30);
    expect(screen.getByText(/4 items \(13 pts\) finished in the last 30 days/i)).toBeInTheDocument();
    expect(screen.getByText(/median 5 d · 85% within 9 d/)).toBeInTheDocument();
    expect(screen.getByRole('cell', { name: 'Dana' })).toBeInTheDocument();
    expect(screen.getByRole('cell', { name: 'Unassigned' })).toBeInTheDocument();
    expect(screen.getByRole('img', { name: /cumulative flow over 2 days/i })).toBeInTheDocument();
  });

  it('reloads with the chosen period', async () => {
    const api = makeApi();
    const user = userEvent.setup();
    renderReports(api);
    await screen.findByText('Cumulative flow');

    await user.click(screen.getByRole('button', { name: '90 days' }));

    await waitFor(() => expect(api.getFlowMetrics).toHaveBeenLastCalledWith(5, 90));
    expect(api.getCumulativeFlow).toHaveBeenLastCalledWith(5, 90);
  });

  it('shows a placeholder instead of an empty chart when nothing finished', async () => {
    renderReports(makeApi({
      getFlowMetrics: vi.fn().mockResolvedValue({
        days: 30, completedCount: 0, completedPoints: 0, weeks: [],
        leadTime: { samples: 0, average: null, median: null, p85: null },
        cycleTime: { samples: 0, average: null, median: null, p85: null },
      }),
    }));

    expect(await screen.findByText(/nothing has been finished in this period/i)).toBeInTheDocument();
  });

  it('exports the work items as CSV', async () => {
    const api = makeApi();
    const user = userEvent.setup();
    renderReports(api);
    await screen.findByText('Cumulative flow');

    await user.click(screen.getByRole('button', { name: /export work items/i }));

    await waitFor(() => expect(api.exportWorkItemsCsv).toHaveBeenCalledWith(5));
  });

  it('offers a retry when the reports fail to load', async () => {
    const api = makeApi({ getWorkloadReport: vi.fn().mockRejectedValue(new Error('boom')) });
    renderReports(api);
    expect(await screen.findByText(/boom/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /try again/i })).toBeInTheDocument();
  });
});
