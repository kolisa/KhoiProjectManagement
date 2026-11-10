import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { ToastProvider } from '../../contexts/ToastContext';
import { ConfirmProvider } from '../../contexts/ConfirmContext';
import PmWorkspace, { parseHash, formatHash } from './PmWorkspace';

// The tabs themselves are covered by their own tests; here only the workspace's own job matters - the project list,
// the "you are here" header, address-bar sync and read-only handling - so each tab's content is a stub that shows
// which project and effective role it was given.
vi.mock('./PmOverview', () => ({ default: ({ project }) => <div data-testid="overview">overview of {project.name}</div> }));
vi.mock('./PmBacklog', () => ({ default: ({ project }) => <div data-testid="backlog">backlog of {project.name} as {project.myRole}</div> }));
vi.mock('./PmBoard', () => ({ default: ({ project }) => <div data-testid="board">board of {project.name}</div> }));
vi.mock('./PmSprints', () => ({ default: ({ project }) => <div data-testid="sprints">sprints of {project.name}</div> }));
vi.mock('./PmReports', () => ({ default: () => <div data-testid="reports" /> }));
vi.mock('./PmAccessAdmin', () => ({ default: () => <div data-testid="access">module access</div> }));
vi.mock('./PmMembers', () => ({
  default: ({ project, currentUserId, canGrantAccess, otherProjects }) => (
    <div data-testid="members">members of {project.name} | me={String(currentUserId)} | grant={String(canGrantAccess)} | others={otherProjects.map((p) => p.name).join(',')}</div>
  ),
}));
vi.mock('./PmMyWork', () => ({
  default: ({ onOpenProject }) => (
    <div data-testid="mywork"><button onClick={() => onOpenProject(2)}>open Beta</button></div>
  ),
}));

const projects = [
  { id: 1, name: 'Alpha', myRole: 'Developer', openItems: 3, totalItems: 9, readOnly: false },
  { id: 2, name: 'Beta', myRole: 'ScrumMaster', openItems: 1, totalItems: 4, readOnly: false },
];

const makeApi = (list = projects, capabilities = undefined) => ({
  getPmProjects: vi.fn().mockResolvedValue(list),
  getPmMembers: vi.fn().mockResolvedValue([]),
  ...(capabilities === undefined ? {} : { getPmCapabilities: vi.fn().mockResolvedValue(capabilities) }),
});

const renderWorkspace = (api, user = { id: 7, permissions: [] }) =>
  render(<ToastProvider><ConfirmProvider><PmWorkspace apiService={api} user={user} /></ConfirmProvider></ToastProvider>);

const nav = () => screen.getByRole('navigation', { name: 'Projects and sections' });
const navItem = (name) => within(nav()).getByRole('button', { name: new RegExp(name) });

describe('address helpers', () => {
  it('round-trips project, section and unknown addresses', () => {
    expect(parseHash('#/pm/3/board')).toEqual({ kind: 'project', projectId: 3, tab: 'board' });
    expect(parseHash('#/pm/3')).toEqual({ kind: 'project', projectId: 3, tab: null });
    expect(parseHash('#/pm/3/nonsense')).toEqual({ kind: 'project', projectId: 3, tab: null });
    expect(parseHash('#/pm/mywork')).toEqual({ kind: 'mywork' });
    expect(parseHash('#/pm/access')).toEqual({ kind: 'access' });
    expect(parseHash('#/something-else')).toBeNull();
    expect(parseHash('')).toBeNull();
    expect(formatHash({ kind: 'project', projectId: 3, tab: 'board' })).toBe('#/pm/3/board');
    expect(formatHash({ kind: 'mywork' })).toBe('#/pm/mywork');
  });
});

describe('PmWorkspace', () => {
  beforeEach(() => {
    try { localStorage.clear(); } catch { /* ignore */ }
    window.history.replaceState(null, '', '/');
    document.title = 'Khoi Pro';
  });

  it('opens on the first project\'s Overview and says exactly where you are', async () => {
    renderWorkspace(makeApi());

    expect(await screen.findByTestId('overview')).toHaveTextContent('overview of Alpha');
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Alpha');
    const crumbs = within(screen.getByRole('navigation', { name: 'Breadcrumb' })).getAllByRole('listitem').map((li) => li.textContent);
    expect(crumbs).toEqual(['Project Management', 'Alpha', 'Overview']);
    expect(screen.getByRole('tab', { name: /Overview/ })).toHaveAttribute('aria-selected', 'true');
    expect(navItem('Alpha')).toHaveAttribute('aria-current', 'page');
    expect(navItem('Beta')).not.toHaveAttribute('aria-current');
    await waitFor(() => expect(document.title).toBe('Alpha · Overview · Project Management'));
  });

  it('shows the person\'s role and what that means they can do', async () => {
    renderWorkspace(makeApi());
    await screen.findByTestId('overview');

    const header = screen.getByRole('heading', { level: 1 }).closest('header');
    expect(within(header).getByText('Developer')).toBeInTheDocument();
    expect(within(header).getByText('Read & write')).toBeInTheDocument();
    expect(within(header).getByText('3 open of 9 items')).toBeInTheDocument();
  });

  it('shows a person with a single project that project straight away - nothing to pick', async () => {
    renderWorkspace(makeApi([projects[1]]));

    expect(await screen.findByTestId('overview')).toHaveTextContent('overview of Beta');
    expect(within(nav()).getAllByRole('listitem').filter((li) => li.textContent.includes('Beta'))).toHaveLength(1);
    expect(navItem('Beta')).toHaveAttribute('aria-current', 'page');
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Beta');
  });

  it('explains the empty case, and still offers My work', async () => {
    const user = userEvent.setup();
    renderWorkspace(makeApi([]));

    expect(await screen.findByText(/not on any project yet/i)).toBeInTheDocument();
    await user.click(navItem('My work'));

    expect(screen.getByTestId('mywork')).toBeInTheDocument();
    expect(window.location.hash).toBe('#/pm/mywork');
  });

  it('keeps the section you are on when you switch project (Board of Alpha -> Board of Beta)', async () => {
    const user = userEvent.setup();
    renderWorkspace(makeApi());
    await screen.findByTestId('overview');
    await user.click(screen.getByRole('tab', { name: /Board/ }));
    expect(await screen.findByTestId('board')).toHaveTextContent('board of Alpha');

    await user.click(navItem('Beta'));

    expect(await screen.findByTestId('board')).toHaveTextContent('board of Beta');
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Beta');
    expect(navItem('Beta')).toHaveAttribute('aria-current', 'page');
    expect(window.location.hash).toBe('#/pm/2/board');
  });

  it('writes the current place into the address bar, and opens straight to an address it is given', async () => {
    window.history.replaceState(null, '', '/#/pm/2/sprints');
    renderWorkspace(makeApi());

    expect(await screen.findByTestId('sprints')).toHaveTextContent('sprints of Beta');
    expect(screen.getByRole('tab', { name: /Sprints/ })).toHaveAttribute('aria-selected', 'true');
    expect(window.location.hash).toBe('#/pm/2/sprints');
  });

  it('puts the starting place into the address bar when it was opened without one', async () => {
    renderWorkspace(makeApi());
    await screen.findByTestId('overview');
    await waitFor(() => expect(window.location.hash).toBe('#/pm/1/overview'));
  });

  it('falls back to a project the person does belong to when the address names one they do not', async () => {
    window.history.replaceState(null, '', '/#/pm/999/board');
    renderWorkspace(makeApi());

    expect(await screen.findByTestId('board')).toHaveTextContent('board of Alpha');
    await waitFor(() => expect(window.location.hash).toBe('#/pm/1/board'));
  });

  it('follows the browser\'s back button', async () => {
    const user = userEvent.setup();
    renderWorkspace(makeApi());
    await screen.findByTestId('overview');
    await user.click(screen.getByRole('tab', { name: /Board/ }));
    await screen.findByTestId('board');

    window.history.back();

    await waitFor(() => expect(screen.getByTestId('overview')).toBeInTheDocument());
    expect(screen.getByRole('tab', { name: /Overview/ })).toHaveAttribute('aria-selected', 'true');
  });

  it('jumps from a My work item to that project\'s backlog, and remembers the project', async () => {
    const user = userEvent.setup();
    renderWorkspace(makeApi());
    await screen.findByTestId('overview');

    await user.click(navItem('My work'));
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('My work');
    await user.click(screen.getByRole('button', { name: 'open Beta' }));

    expect(await screen.findByTestId('backlog')).toHaveTextContent('backlog of Beta');
    expect(localStorage.getItem('khoi_pm_project')).toBe('2');
  });

  it('treats read-only access as Viewer everywhere, while still showing the real role and explaining why', async () => {
    renderWorkspace(makeApi([{ ...projects[0], readOnly: true }]));
    await screen.findByTestId('overview');
    const user = userEvent.setup();

    expect(screen.getByRole('note')).toHaveTextContent(/Read-only access/i);
    const header = screen.getByRole('heading', { level: 1 }).closest('header');
    expect(within(header).getByText('Read only')).toBeInTheDocument();
    expect(within(header).getByText('Developer')).toBeInTheDocument();         // the real role is still shown

    await user.click(screen.getByRole('tab', { name: /Backlog/ }));
    expect(await screen.findByTestId('backlog')).toHaveTextContent('backlog of Alpha as Viewer');   // screens get the capped role
  });

  it('shows no read-only note to someone who can write', async () => {
    renderWorkspace(makeApi());
    await screen.findByTestId('overview');
    expect(screen.queryByRole('note')).not.toBeInTheDocument();
  });

  it('offers Module access only to people holding pm.admin, and turns others away from its address', async () => {
    const { unmount } = renderWorkspace(makeApi(), { id: 7, permissions: ['projects.view'] });
    await screen.findByTestId('overview');
    expect(within(nav()).queryByRole('button', { name: /Module access/ })).not.toBeInTheDocument();
    unmount();

    window.history.replaceState(null, '', '/#/pm/access');
    renderWorkspace(makeApi(), { id: 7, permissions: ['projects.view'] });
    expect(await screen.findByTestId('overview')).toBeInTheDocument();        // redirected to a project, not the admin screen
    expect(screen.queryByTestId('access')).not.toBeInTheDocument();
  });

  it('lets a pm.admin open Module access', async () => {
    const user = userEvent.setup();
    renderWorkspace(makeApi(), { id: 7, permissions: ['pm.admin'] });
    await screen.findByTestId('overview');

    await user.click(navItem('Module access'));

    expect(screen.getByTestId('access')).toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Module access');
  });

  it('hands the members page the signed-in user, the grant ability and the other projects to copy from', async () => {
    const user = userEvent.setup();
    renderWorkspace(makeApi(), { id: 7, permissions: ['pm.admin'] });
    await screen.findByTestId('overview');

    await user.click(screen.getByRole('tab', { name: /Members/ }));

    expect(await screen.findByTestId('members')).toHaveTextContent('members of Alpha | me=7 | grant=true | others=Beta');
  });

  it('collapses the project list behind one button that names the current place on small screens', async () => {
    const user = userEvent.setup();
    renderWorkspace(makeApi());
    await screen.findByTestId('overview');

    const toggle = screen.getByRole('button', { name: /You are in/ });
    expect(toggle).toHaveTextContent('Alpha');
    expect(toggle).toHaveAttribute('aria-expanded', 'false');
    await user.click(toggle);
    expect(toggle).toHaveAttribute('aria-expanded', 'true');

    await user.click(navItem('Beta'));
    expect(screen.getByRole('button', { name: /You are in/ })).toHaveTextContent('Beta');
    expect(screen.getByRole('button', { name: /You are in/ })).toHaveAttribute('aria-expanded', 'false');   // closes after choosing
  });

  it('offers a search box only when there are many projects', async () => {
    renderWorkspace(makeApi());
    await screen.findByTestId('overview');
    expect(screen.queryByLabelText('Find a project')).not.toBeInTheDocument();
  });

  it('finds a project by name when there are many', async () => {
    const many = Array.from({ length: 9 }, (_, i) => ({ id: i + 1, name: i === 7 ? 'Zebra launch' : `Project ${i + 1}`, myRole: 'Developer', openItems: i, totalItems: 10, readOnly: false }));
    const user = userEvent.setup();
    renderWorkspace(makeApi(many));
    await screen.findByTestId('overview');

    await user.type(screen.getByLabelText('Find a project'), 'zeb');

    expect(within(nav()).getByRole('button', { name: /Zebra launch/ })).toBeInTheDocument();
    expect(within(nav()).queryByRole('button', { name: /Project 3/ })).not.toBeInTheDocument();
  });

  it('offers a retry when the projects fail to load', async () => {
    const api = { getPmProjects: vi.fn().mockRejectedValue(new Error('boom')), getPmMembers: vi.fn() };
    renderWorkspace(api);
    expect(await screen.findByText(/boom/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /try again/i })).toBeInTheDocument();
  });

  describe('who counts as an administrator', () => {
    it('lets a Project Admin - who holds no pm.admin permission - open Module access, because the server says they are an administrator', async () => {
      const user = userEvent.setup();
      renderWorkspace(makeApi(projects, { isAdmin: true }), { id: 7, permissions: [] });
      await screen.findByTestId('overview');

      await user.click(navItem('Module access'));

      expect(screen.getByTestId('access')).toBeInTheDocument();
    });

    it('also tells the members page that this person may grant module access', async () => {
      const user = userEvent.setup();
      renderWorkspace(makeApi(projects, { isAdmin: true }), { id: 7, permissions: [] });
      await screen.findByTestId('overview');

      await user.click(screen.getByRole('tab', { name: /Members/ }));

      expect(await screen.findByTestId('members')).toHaveTextContent('grant=true');
    });

    it('trusts the server over the permission list: no longer an administrator means no Module access', async () => {
      renderWorkspace(makeApi(projects, { isAdmin: false }), { id: 7, permissions: ['pm.admin'] });
      await screen.findByTestId('overview');
      expect(within(nav()).queryByRole('button', { name: /Module access/ })).not.toBeInTheDocument();
    });

    it('falls back to the permission list if the capabilities request fails', async () => {
      const api = makeApi(projects);
      api.getPmCapabilities = vi.fn().mockRejectedValue(new Error('offline'));
      renderWorkspace(api, { id: 7, permissions: ['pm.admin'] });
      await screen.findByTestId('overview');
      expect(within(nav()).getByRole('button', { name: /Module access/ })).toBeInTheDocument();
    });

    it('shows a SuperAdmin role as Administrator', async () => {
      renderWorkspace(makeApi([{ ...projects[0], myRole: 'SuperAdmin' }], { isAdmin: true }));
      await screen.findByTestId('overview');
      const header = screen.getByRole('heading', { level: 1 }).closest('header');
      expect(within(header).getByText('Administrator')).toBeInTheDocument();
      expect(within(header).getByText('Full control')).toBeInTheDocument();
    });
  });
});
