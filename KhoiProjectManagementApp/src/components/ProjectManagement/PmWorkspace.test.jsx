import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { ToastProvider } from '../../contexts/ToastContext';
import { ConfirmProvider } from '../../contexts/ConfirmContext';
import PmWorkspace from './PmWorkspace';

// The tabs themselves are covered by their own tests; here only the workspace's wiring matters, so each
// tab's content is replaced with a stub that shows which project it was given.
vi.mock('./PmOverview', () => ({ default: ({ project }) => <div data-testid="overview">overview of {project.name}</div> }));
vi.mock('./PmBacklog', () => ({ default: ({ project }) => <div data-testid="backlog">backlog of {project.name}</div> }));
vi.mock('./PmBoard', () => ({ default: ({ project }) => <div data-testid="board">board of {project.name}</div> }));
vi.mock('./PmSprints', () => ({ default: () => <div data-testid="sprints" /> }));
vi.mock('./PmReports', () => ({ default: () => <div data-testid="reports" /> }));
vi.mock('./PmMembers', () => ({
  default: ({ project, currentUserId, canGrantAccess, otherProjects }) => (
    <div data-testid="members">members of {project.name} | me={String(currentUserId)} | grant={String(canGrantAccess)} | others={otherProjects.map((p) => p.name).join(',')}</div>
  ),
}));
vi.mock('./PmAccessAdmin', () => ({ default: () => <div data-testid="access">module access</div> }));
vi.mock('./PmMyWork', () => ({
  default: ({ onOpenProject }) => (
    <div data-testid="mywork">
      <button onClick={() => onOpenProject(2)}>open Beta</button>
    </div>
  ),
}));

const projects = [
  { id: 1, name: 'Alpha', myRole: 'Developer', openItems: 3, totalItems: 9 },
  { id: 2, name: 'Beta', myRole: 'ScrumMaster', openItems: 1, totalItems: 4 },
];

const makeApi = (list = projects) => ({
  getPmProjects: vi.fn().mockResolvedValue(list),
  getPmMembers: vi.fn().mockResolvedValue([]),
});

const renderWorkspace = (api, user = { id: 7, permissions: [] }) =>
  render(<ToastProvider><ConfirmProvider><PmWorkspace apiService={api} user={user} /></ConfirmProvider></ToastProvider>);

describe('PmWorkspace wiring', () => {
  beforeEach(() => { try { localStorage.clear(); } catch { /* ignore */ } });

  it('opens on the Overview tab of the first project, with Overview and My work leading the tab list', async () => {
    renderWorkspace(makeApi());

    expect(await screen.findByTestId('overview')).toHaveTextContent('overview of Alpha');
    const tabs = screen.getAllByRole('tab').map((t) => t.textContent);
    expect(tabs.slice(0, 3)).toEqual(['Overview', 'My work', 'Backlog']);
    expect(screen.getByRole('tab', { name: 'Overview' })).toHaveAttribute('aria-selected', 'true');
  });

  it('shows My work even when the person is on no project at all', async () => {
    const user = userEvent.setup();
    renderWorkspace(makeApi([]));
    expect(await screen.findByText(/not a member of any project/i)).toBeInTheDocument();

    await user.click(screen.getByRole('tab', { name: 'My work' }));

    expect(screen.getByTestId('mywork')).toBeInTheDocument();
  });

  it('jumps from a My work item to that project\'s backlog, and remembers the project', async () => {
    const user = userEvent.setup();
    renderWorkspace(makeApi());
    await screen.findByTestId('overview');

    await user.click(screen.getByRole('tab', { name: 'My work' }));
    await user.click(screen.getByRole('button', { name: 'open Beta' }));

    expect(await screen.findByTestId('backlog')).toHaveTextContent('backlog of Beta');
    expect(screen.getByRole('tab', { name: 'Backlog' })).toHaveAttribute('aria-selected', 'true');
    expect(localStorage.getItem('khoi_pm_project')).toBe('2');
  });

  it('keeps the chosen tab when the project is switched', async () => {
    const user = userEvent.setup();
    renderWorkspace(makeApi());
    await screen.findByTestId('overview');
    await user.click(screen.getByRole('tab', { name: 'Board' }));
    expect(await screen.findByTestId('board')).toHaveTextContent('board of Alpha');

    await user.selectOptions(screen.getByLabelText('Project'), '2');

    await waitFor(() => expect(screen.getByTestId('board')).toHaveTextContent('board of Beta'));
  });

  it('offers the Module access tab only to people holding pm.admin', async () => {
    const { unmount } = renderWorkspace(makeApi(), { id: 7, permissions: ['projects.view'] });
    await screen.findByTestId('overview');
    expect(screen.queryByRole('tab', { name: 'Module access' })).not.toBeInTheDocument();
    unmount();

    renderWorkspace(makeApi(), { id: 7, permissions: ['pm.admin'] });
    await screen.findByTestId('overview');
    expect(screen.getByRole('tab', { name: 'Module access' })).toBeInTheDocument();
  });

  it('hands the members page the signed-in user, the grant ability and the other projects to copy from', async () => {
    const user = userEvent.setup();
    renderWorkspace(makeApi(), { id: 7, permissions: ['pm.admin'] });
    await screen.findByTestId('overview');

    await user.click(screen.getByRole('tab', { name: 'Members' }));

    expect(await screen.findByTestId('members')).toHaveTextContent('members of Alpha | me=7 | grant=true | others=Beta');
  });
});
