import { render, screen, waitFor, within, fireEvent } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';
import { ToastProvider } from '../../contexts/ToastContext';
import { ConfirmProvider } from '../../contexts/ConfirmContext';
import PmBoard from './PmBoard';
import PmSprints from './PmSprints';
import PmBacklog from './PmBacklog';

vi.mock('./PmCharts', () => ({ BurndownChart: () => <div />, VelocityChart: () => <div /> }));
vi.mock('./SprintEffort', () => ({ default: () => <div /> }));

const wrap = (ui) => render(<ToastProvider><ConfirmProvider>{ui}</ConfirmProvider></ToastProvider>);
const members = [{ userId: 1, name: 'Ada Lovelace', email: 'a@x.com' }, { userId: 2, name: 'Ben Carter', email: 'b@x.com' }, { userId: 3, name: 'Cleo Park', email: 'c@x.com' }];
const project = { id: 5, name: 'Khoi', myRole: 'ScrumMaster', actualRole: 'ScrumMaster' };
const viewer = { ...project, myRole: 'Viewer', actualRole: 'Viewer' };

const sprint = (over) => ({ id: 1, projectId: 5, name: 'Sprint 1', goal: 'Ship login', state: 'Active', startDate: '2026-11-01', endDate: '2099-11-14', totalPoints: 10, donePoints: 4, itemCount: 2, ...over });

const columns = (items) => ({
  projectId: 5,
  columns: [
    { id: 1, name: 'To do', status: 'todo', count: 0, wipLimit: null, overWip: false, items: items.filter((i) => i.status === 'todo') },
    { id: 2, name: 'Done', status: 'completed', count: 0, wipLimit: null, overWip: false, items: items.filter((i) => i.status === 'completed') },
  ],
});

const card = (id, title, assignedToId, assignedToName, status = 'todo') => ({ id, title, assignedToId, assignedToName, status, type: 'Story', priority: 'medium', labels: [] });

const boardApi = (sprints, cards = [card(10, 'Login page', 1, 'Ada Lovelace'), card(11, 'Logout', 2, 'Ben Carter'), card(12, 'Docs', null, null)]) => ({
  getSprints: vi.fn().mockResolvedValue(sprints),
  getBoard: vi.fn().mockImplementation(async () => columns(cards)),
  getWorkflow: vi.fn().mockResolvedValue(null),
  createSprint: vi.fn().mockResolvedValue({ id: 9, name: 'Sprint 9' }),
  startSprint: vi.fn().mockResolvedValue({}),
  moveWorkItem: vi.fn().mockResolvedValue({}),
});

describe('PmBoard people and sprints', () => {
  it('opens on the active sprint, like a Scrum board', async () => {
    const api = boardApi([sprint({ id: 1 }), sprint({ id: 2, name: 'Sprint 2', state: 'Planned' })]);
    wrap(<PmBoard apiService={api} project={project} members={members} />);

    await screen.findByText('Login page');
    expect(api.getBoard).toHaveBeenCalledWith(5, '1');
    expect(screen.getByLabelText('Show work from')).toHaveTextContent('Sprint 1');
    expect(screen.getByRole('region', { name: 'Sprint 1 summary' })).toHaveTextContent('Ship login');
  });

  it('shows all work (plain Kanban) when no sprint is active', async () => {
    const api = boardApi([sprint({ state: 'Planned' })]);
    wrap(<PmBoard apiService={api} project={project} members={members} />);
    await screen.findByText('Login page');
    expect(api.getBoard).toHaveBeenCalledWith(5, null);
    expect(screen.getByLabelText('Show work from')).toHaveTextContent('All work (Kanban)');
  });

  it('shows avatars for the people on the board and filters cards when one is clicked', async () => {
    const user = userEvent.setup();
    wrap(<PmBoard apiService={boardApi([])} project={project} members={members} />);
    await screen.findByText('Login page');

    const bar = screen.getByRole('group', { name: 'Filter by person' });
    expect(within(bar).getByRole('button', { name: 'Ada Lovelace, 1 card' })).toBeInTheDocument();
    expect(within(bar).getByRole('button', { name: 'Ben Carter, 1 card' })).toBeInTheDocument();
    expect(within(bar).queryByRole('button', { name: /Cleo Park/ })).not.toBeInTheDocument();   // nothing assigned to Cleo

    await user.click(within(bar).getByRole('button', { name: 'Ada Lovelace, 1 card' }));
    expect(screen.getByText('Login page')).toBeInTheDocument();
    expect(screen.queryByText('Logout')).not.toBeInTheDocument();
    expect(screen.queryByText('Docs')).not.toBeInTheDocument();

    await user.click(within(bar).getByRole('button', { name: 'Unassigned, 1 card' }));          // add a second person to the filter
    expect(screen.getByText('Docs')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Clear filter' }));
    expect(screen.getByText('Logout')).toBeInTheDocument();
  });

  it('puts the assignee avatar on each card', async () => {
    wrap(<PmBoard apiService={boardApi([])} project={project} members={members} />);
    await screen.findByText('Login page');
    expect(screen.getAllByText('Assigned to Ada Lovelace').length).toBeGreaterThan(0);
    expect(screen.getAllByText('Unassigned').length).toBeGreaterThan(0);
  });

  it('creates a sprint right from the board and switches to it', async () => {
    const api = boardApi([]);
    const user = userEvent.setup();
    wrap(<PmBoard apiService={api} project={project} members={members} />);
    await screen.findByText('Login page');

    await user.click(screen.getByRole('button', { name: /new sprint/i }));
    await user.type(await screen.findByLabelText(/name/i), 'Sprint 9');
    await user.click(screen.getByRole('button', { name: /^(create|save)/i }));

    await waitFor(() => expect(api.createSprint).toHaveBeenCalledWith(5, expect.objectContaining({ name: 'Sprint 9' })));
  });

  it('starts a planned sprint from the board when nothing else is active', async () => {
    const api = boardApi([sprint({ id: 4, name: 'Next', state: 'Planned' })]);
    const user = userEvent.setup();
    wrap(<PmBoard apiService={api} project={project} members={members} />);
    await screen.findByText('Login page');

    await user.click(screen.getByLabelText('Show work from'));
    await user.click(screen.getByRole('option', { name: /Next/ }));
    await user.click(await screen.findByRole('button', { name: /start sprint/i }));

    await waitFor(() => expect(api.startSprint).toHaveBeenCalledWith(4));
  });

  it('greys out New sprint, with the reason, for a Viewer', async () => {
    wrap(<PmBoard apiService={boardApi([])} project={viewer} members={members} />);
    await screen.findByText('Login page');
    expect(screen.getByRole('button', { name: /new sprint/i })).toBeDisabled();
  });
});

describe('PmSprints planning', () => {
  const items = [
    { id: 1, title: 'Alpha', type: 'Story', status: 'todo', sprintId: null, assignedToName: 'Ada Lovelace', storyPoints: 3 },
    { id: 2, title: 'Beta', type: 'Task', status: 'todo', sprintId: null, storyPoints: 2 },
    { id: 3, title: 'Gamma', type: 'Task', status: 'todo', sprintId: 1, storyPoints: 5 },
  ];
  const sprintsApi = () => ({
    getSprints: vi.fn().mockResolvedValue([sprint({ id: 1, name: 'Sprint 1' }), sprint({ id: 2, name: 'Sprint 2', state: 'Planned' })]),
    getWorkItems: vi.fn().mockResolvedValue(items),
    getVelocity: vi.fn().mockResolvedValue({ sprints: [] }),
    getBurndown: vi.fn().mockResolvedValue(null),
    assignWorkItemSprint: vi.fn().mockResolvedValue({}),
  });

  it('moves one item with a searchable "move to" picker', async () => {
    const api = sprintsApi();
    const user = userEvent.setup();
    wrap(<PmSprints apiService={api} project={project} />);
    await screen.findByText('Alpha');

    await user.click(screen.getByLabelText('Sprint for Alpha'));
    await user.type(screen.getByLabelText('Search options'), 'sprint 2');
    await user.click(screen.getByRole('option', { name: /Sprint 2/ }));

    await waitFor(() => expect(api.assignWorkItemSprint).toHaveBeenCalledWith(1, 2));
  });

  it('moves several ticked items into a sprint in one go', async () => {
    const api = sprintsApi();
    const user = userEvent.setup();
    wrap(<PmSprints apiService={api} project={project} />);
    await screen.findByText('Alpha');

    await user.click(screen.getByRole('checkbox', { name: 'Select Alpha' }));
    await user.click(screen.getByRole('checkbox', { name: 'Select Beta' }));
    const bar = screen.getByRole('region', { name: 'Move selected items' });
    expect(bar).toHaveTextContent('2 selected');
    await user.click(within(bar).getByLabelText('Move selected items to'));
    await user.click(screen.getByRole('option', { name: /Sprint 1/ }));

    await waitFor(() => expect(api.assignWorkItemSprint).toHaveBeenCalledTimes(2));
    expect(api.assignWorkItemSprint).toHaveBeenCalledWith(1, 1);
    expect(api.assignWorkItemSprint).toHaveBeenCalledWith(2, 1);
  });

  it('selects the whole backlog at once', async () => {
    const user = userEvent.setup();
    wrap(<PmSprints apiService={sprintsApi()} project={project} />);
    await screen.findByText('Alpha');
    await user.click(screen.getByRole('checkbox', { name: 'Select all backlog items' }));
    expect(screen.getByRole('region', { name: 'Move selected items' })).toHaveTextContent('2 selected');
  });

  it('moves an item by dragging it onto a sprint', async () => {
    const api = sprintsApi();
    wrap(<PmSprints apiService={api} project={project} />);
    const row = (await screen.findByText('Beta')).closest('li');
    const target = screen.getByRole('region', { name: 'Sprint 2' });

    fireEvent.dragStart(row);
    fireEvent.dragOver(target);
    fireEvent.drop(target);

    await waitFor(() => expect(api.assignWorkItemSprint).toHaveBeenCalledWith(2, 2));
  });

  it('drags a whole selection when the dragged item is part of it', async () => {
    const api = sprintsApi();
    const user = userEvent.setup();
    wrap(<PmSprints apiService={api} project={project} />);
    await user.click(await screen.findByRole('checkbox', { name: 'Select Alpha' }));
    await user.click(screen.getByRole('checkbox', { name: 'Select Beta' }));

    const row = screen.getByText('Alpha').closest('li');
    const target = screen.getByRole('region', { name: 'Sprint 2' });
    fireEvent.dragStart(row);
    fireEvent.dragOver(target);
    fireEvent.drop(target);

    await waitFor(() => expect(api.assignWorkItemSprint).toHaveBeenCalledTimes(2));
  });

  it('sends an item back to the backlog by dropping it there', async () => {
    const api = sprintsApi();
    wrap(<PmSprints apiService={api} project={project} />);
    const row = (await screen.findByText('Gamma')).closest('li');
    const target = screen.getByRole('region', { name: 'Backlog' });

    fireEvent.dragStart(row);
    fireEvent.dragOver(target);
    fireEvent.drop(target);

    await waitFor(() => expect(api.assignWorkItemSprint).toHaveBeenCalledWith(3, null));
  });

  it('cannot plan at all for a Viewer: everything is greyed out', async () => {
    wrap(<PmSprints apiService={sprintsApi()} project={viewer} />);
    await screen.findByText('Alpha');
    expect(screen.getByLabelText('Sprint for Alpha')).toBeDisabled();
    expect(screen.getByRole('checkbox', { name: 'Select Alpha' })).toBeDisabled();
  });
});

describe('PmBacklog add to sprint', () => {
  const rows = [
    { id: 1, title: 'Alpha', type: 'Story', status: 'todo', priority: 'medium', sprintId: null, labels: [] },
    { id: 2, title: 'Big epic', type: 'Epic', status: 'todo', priority: 'medium', sprintId: null, labels: [] },
  ];
  const api = () => ({
    getWorkItems: vi.fn().mockResolvedValue(rows),
    getProjectLabels: vi.fn().mockResolvedValue([]),
    getSprints: vi.fn().mockResolvedValue([sprint({ id: 1 }), sprint({ id: 2, name: 'Sprint 2', state: 'Planned' })]),
    assignWorkItemSprint: vi.fn().mockResolvedValue({}),
  });

  it('adds an item to a sprint straight from the backlog row', async () => {
    const a = api();
    const user = userEvent.setup();
    wrap(<PmBacklog apiService={a} project={project} members={members} />);
    await screen.findByText('Alpha');

    await user.click(screen.getByLabelText('Sprint for Alpha'));
    await user.click(screen.getByRole('option', { name: /Sprint 2/ }));

    await waitFor(() => expect(a.assignWorkItemSprint).toHaveBeenCalledWith(1, 2));
  });

  it('offers no sprint picker for an Epic - containers are planned through their stories', async () => {
    wrap(<PmBacklog apiService={api()} project={project} members={members} />);
    await screen.findByText('Big epic');
    expect(screen.queryByLabelText('Sprint for Big epic')).not.toBeInTheDocument();
  });
});
