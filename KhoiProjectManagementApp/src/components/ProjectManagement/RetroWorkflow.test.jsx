import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';
import { ToastProvider } from '../../contexts/ToastContext';
import { ConfirmProvider } from '../../contexts/ConfirmContext';
import RetroModal from './RetroModal';
import WorkflowModal from './WorkflowModal';

const wrap = (ui) => render(<ToastProvider><ConfirmProvider>{ui}</ConfirmProvider></ToastProvider>);

const card = (over) => ({ id: 1, sprintId: 9, category: 'WentWell', text: 'Good demos', authorName: 'Dana', isDone: false, canEdit: false, canDelete: false, ...over });

const makeApi = (items) => ({
  getRetro: vi.fn().mockResolvedValue(items),
  addRetroItem: vi.fn().mockResolvedValue({}),
  updateRetroItem: vi.fn().mockResolvedValue({}),
  setRetroItemDone: vi.fn().mockResolvedValue({}),
  deleteRetroItem: vi.fn().mockResolvedValue(null),
});

describe('RetroModal', () => {
  const items = [
    card({ id: 1, category: 'WentWell', text: 'Good demos', canEdit: true, canDelete: true }),
    card({ id: 2, category: 'ToImprove', text: 'Slow reviews', authorName: 'Sam' }),
    card({ id: 3, category: 'Action', text: 'Review within a day', authorName: 'Sam' }),
  ];

  it('files each card under its column', async () => {
    wrap(<RetroModal apiService={makeApi(items)} sprint={{ id: 9, name: 'Sprint 4' }} canAdd onClose={vi.fn()} />);

    const went = await screen.findByRole('region', { name: 'Went well' });
    expect(within(went).getByText('Good demos')).toBeInTheDocument();
    expect(within(screen.getByRole('region', { name: 'To improve' })).getByText('Slow reviews')).toBeInTheDocument();
    expect(within(screen.getByRole('region', { name: 'Actions' })).getByText('Review within a day')).toBeInTheDocument();
  });

  it('adds a trimmed card to the chosen column', async () => {
    const api = makeApi(items);
    const user = userEvent.setup();
    wrap(<RetroModal apiService={api} sprint={{ id: 9, name: 'Sprint 4' }} canAdd onClose={vi.fn()} />);

    await user.type(await screen.findByLabelText('Add to To improve'), '  Flaky tests ');
    await user.click(within(screen.getByRole('region', { name: 'To improve' })).getByRole('button', { name: 'Add' }));

    await waitFor(() => expect(api.addRetroItem).toHaveBeenCalledWith(9, 'ToImprove', 'Flaky tests'));
  });

  it('ticks an action off, and only offers edit/delete where the server allows', async () => {
    const api = makeApi(items);
    const user = userEvent.setup();
    wrap(<RetroModal apiService={api} sprint={{ id: 9, name: 'Sprint 4' }} canAdd onClose={vi.fn()} />);

    await user.click(await screen.findByRole('checkbox', { name: /mark done: review within a day/i }));
    await waitFor(() => expect(api.setRetroItemDone).toHaveBeenCalledWith(3, true));

    expect(screen.getAllByRole('button', { name: 'Delete card' })).toHaveLength(1); // only the author's card
  });

  it('greys out the add inputs for read-only roles', async () => {
    wrap(<RetroModal apiService={makeApi(items)} sprint={{ id: 9, name: 'Sprint 4' }} canAdd={false} addReason="Your access is read-only." onClose={vi.fn()} />);
    await screen.findByText('Good demos');
    const inputs = screen.getAllByLabelText(/^Add to/);
    expect(inputs.length).toBeGreaterThan(0);
    inputs.forEach((i) => expect(i).toBeDisabled());
  });
});

describe('WorkflowModal', () => {
  const statuses = ['todo', 'in-progress', 'blocked', 'completed'];
  const workflow = {
    statuses,
    isCustomised: false,
    transitions: statuses.flatMap((from) => statuses.filter((to) => to !== from).map((to) => ({ from, to, allowed: true }))),
  };

  it('saves the full matrix with an unticked move marked disallowed', async () => {
    const onSave = vi.fn().mockResolvedValue(undefined);
    const user = userEvent.setup();
    render(<WorkflowModal workflow={workflow} onSave={onSave} onClose={vi.fn()} />);

    await user.click(screen.getByRole('checkbox', { name: 'Allow To do to Done' }));
    await user.click(screen.getByRole('button', { name: 'Save' }));

    await waitFor(() => expect(onSave).toHaveBeenCalledTimes(1));
    const sent = onSave.mock.calls[0][0];
    expect(sent).toHaveLength(12);
    expect(sent.find((t) => t.from === 'todo' && t.to === 'completed').allowed).toBe(false);
    expect(sent.filter((t) => !t.allowed)).toHaveLength(1);
  });

  it('shows the server error and stays open when the rule set is refused', async () => {
    const onSave = vi.fn().mockRejectedValue(new Error('must still be able to reach Done'));
    const user = userEvent.setup();
    render(<WorkflowModal workflow={workflow} onSave={onSave} onClose={vi.fn()} />);

    await user.click(screen.getByRole('button', { name: 'Save' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(/reach Done/);
  });

  it('"Allow all moves" re-ticks everything', async () => {
    const custom = { ...workflow, transitions: workflow.transitions.map((t) => ({ ...t, allowed: t.from !== 'todo' })) };
    const user = userEvent.setup();
    render(<WorkflowModal workflow={custom} onSave={vi.fn()} onClose={vi.fn()} />);
    expect(screen.getByRole('checkbox', { name: 'Allow To do to Blocked' })).not.toBeChecked();

    await user.click(screen.getByRole('button', { name: 'Allow all moves' }));

    expect(screen.getByRole('checkbox', { name: 'Allow To do to Blocked' })).toBeChecked();
  });
});
