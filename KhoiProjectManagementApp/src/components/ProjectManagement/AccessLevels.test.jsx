import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';
import { ToastProvider } from '../../contexts/ToastContext';
import { ConfirmProvider } from '../../contexts/ConfirmContext';
import { accessLevel, effectiveProject, roleCan, ABILITY_ROWS, can } from './pmConstants';
import { AccessBadge, Avatar } from './pmUi';
import RoleMatrix from './RoleMatrix';
import PmAccessAdmin, { levelOf } from './PmAccessAdmin';
import PmMembers from './PmMembers';
import ImportMembersModal from './ImportMembersModal';
import PmProjectNav from './PmProjectNav';

const wrap = (ui) => render(<ToastProvider><ConfirmProvider>{ui}</ConfirmProvider></ToastProvider>);

describe('access level helpers', () => {
  it('describes the combination of role and module level in one word', () => {
    expect(accessLevel('Viewer')).toBe('read');
    expect(accessLevel('Developer')).toBe('write');
    expect(accessLevel('Tester')).toBe('write');
    expect(accessLevel('ScrumMaster')).toBe('write');
    expect(accessLevel('ProjectAdmin')).toBe('full');
    expect(accessLevel('SuperAdmin')).toBe('full');
    expect(accessLevel(undefined)).toBe('read');
  });

  it('lets a read-only module level win over any role - except a SuperAdmin', () => {
    expect(accessLevel('ProjectAdmin', true)).toBe('read');
    expect(accessLevel('Developer', true)).toBe('read');
    expect(accessLevel('SuperAdmin', true)).toBe('full');
  });

  it('caps the abilities the screens see when the module level is read only', () => {
    const readOnly = effectiveProject({ id: 1, myRole: 'ProjectAdmin', readOnly: true });
    expect(readOnly.myRole).toBe('Viewer');
    expect(readOnly.actualRole).toBe('ProjectAdmin');
    expect(can(readOnly.myRole, 'edit')).toBe(false);
    expect(can(readOnly.myRole, 'members')).toBe(false);

    const normal = effectiveProject({ id: 1, myRole: 'Developer', readOnly: false });
    expect(normal.myRole).toBe('Developer');
    expect(effectiveProject(null)).toBeNull();
  });

  it('answers the role matrix from the same table the buttons use', () => {
    expect(roleCan('Viewer', 'view')).toBe(true);
    expect(roleCan('Viewer', 'edit')).toBe(false);
    expect(roleCan('Tester', 'collaborate')).toBe(true);
    expect(roleCan('Developer', 'delete')).toBe(false);
    expect(roleCan('ProjectAdmin', 'members')).toBe(true);
    expect(ABILITY_ROWS.length).toBeGreaterThan(5);
  });
});

describe('badges', () => {
  it('AccessBadge says Read only / Read & write / Full control', () => {
    const { rerender } = render(<AccessBadge role="Viewer" />);
    expect(screen.getByText('Read only')).toBeInTheDocument();
    rerender(<AccessBadge role="Developer" />);
    expect(screen.getByText('Read & write')).toBeInTheDocument();
    rerender(<AccessBadge role="ProjectAdmin" />);
    expect(screen.getByText('Full control')).toBeInTheDocument();
    rerender(<AccessBadge role="ProjectAdmin" readOnly />);
    expect(screen.getByText('Read only')).toBeInTheDocument();
  });

  it('Avatar shows initials, hidden from assistive technology', () => {
    const { container } = render(<Avatar name="Dana Dev" />);
    expect(container.firstChild).toHaveTextContent('DD');
    expect(container.firstChild).toHaveAttribute('aria-hidden', 'true');
  });
});

describe('RoleMatrix', () => {
  it('lists every role against every ability and highlights the viewer\'s own role', () => {
    render(<RoleMatrix highlightRole="Tester" />);
    const table = screen.getByRole('table', { name: /what each project role is allowed to do/i });
    expect(within(table).getAllByRole('columnheader')).toHaveLength(7);   // "Ability" + six roles
    const deleteRow = within(table).getByRole('row', { name: /Delete work items/ });
    expect(within(deleteRow).getAllByLabelText('Allowed')).toHaveLength(3);   // Admin, Manager, Scrum Master
    expect(within(table).getByRole('columnheader', { name: 'Tester' })).toHaveClass('bg-blue-50');
    expect(screen.getByText(/can set someone's module access to/i)).toBeInTheDocument();
  });
});

describe('PmAccessAdmin', () => {
  const people = [
    { id: 1, name: 'Ada Admin', email: 'ada@x.com', pmAccess: true, pmOnly: false, pmReadOnly: false },
    { id: 2, name: 'Rae Reader', email: 'rae@x.com', pmAccess: true, pmOnly: false, pmReadOnly: true },
    { id: 3, name: 'Nia None', email: 'nia@x.com', pmAccess: false, pmOnly: false, pmReadOnly: false, projectCount: 2 },
  ];
  const makeApi = () => ({
    getPmAccessUsers: vi.fn().mockResolvedValue(people),
    setPmAccess: vi.fn().mockResolvedValue(null),
  });

  it('derives each person\'s level from the flags', () => {
    expect(people.map(levelOf)).toEqual(['write', 'read', 'none']);
  });

  it('shows each person\'s level, and explains what the levels mean', async () => {
    wrap(<PmAccessAdmin apiService={makeApi()} />);

    expect(await screen.findByLabelText('Access level for Ada Admin')).toHaveValue('write');
    expect(screen.getByLabelText('Access level for Rae Reader')).toHaveValue('read');
    expect(screen.getByLabelText('Access level for Nia None')).toHaveValue('none');
    const explainer = screen.getByRole('region', { name: 'Access levels explained' });
    expect(explainer).toHaveTextContent(/change nothing, whatever their project role/i);
  });

  it('sets read only, read & write, or no access', async () => {
    const api = makeApi();
    const user = userEvent.setup();
    wrap(<PmAccessAdmin apiService={api} />);
    await screen.findByLabelText('Access level for Ada Admin');

    await user.selectOptions(screen.getByLabelText('Access level for Ada Admin'), 'read');
    await waitFor(() => expect(api.setPmAccess).toHaveBeenLastCalledWith(1, true, false, true));

    await user.selectOptions(screen.getByLabelText('Access level for Rae Reader'), 'write');
    await waitFor(() => expect(api.setPmAccess).toHaveBeenLastCalledWith(2, true, false, false));

    await user.selectOptions(screen.getByLabelText('Access level for Nia None'), 'read');
    await waitFor(() => expect(api.setPmAccess).toHaveBeenLastCalledWith(3, true, false, true));

    await user.selectOptions(screen.getByLabelText('Access level for Ada Admin'), 'none');
    await waitFor(() => expect(api.setPmAccess).toHaveBeenLastCalledWith(1, false, false, false));
  });

  it('drops the confinement when access is removed, and only offers it to people who have access', async () => {
    const api = makeApi();
    api.getPmAccessUsers.mockResolvedValue([{ ...people[0], pmOnly: true }, people[2]]);
    const user = userEvent.setup();
    wrap(<PmAccessAdmin apiService={api} />);

    expect(await screen.findByLabelText('Confine Nia None to the Project Management module')).toBeDisabled();
    await user.selectOptions(screen.getByLabelText('Access level for Ada Admin'), 'none');

    await waitFor(() => expect(api.setPmAccess).toHaveBeenCalledWith(1, false, false, false));
  });

  it('flags someone who is on projects but has no access, so an admin can see why they see nothing', async () => {
    wrap(<PmAccessAdmin apiService={makeApi()} />);
    await screen.findByLabelText('Access level for Nia None');

    const row = screen.getByLabelText('Access level for Nia None').closest('tr');
    expect(within(row).getByText(/can't see them/i)).toBeInTheDocument();
    const adaRow = screen.getByLabelText('Access level for Ada Admin').closest('tr');
    expect(within(adaRow).queryByText(/can't see them/i)).not.toBeInTheDocument();
  });

  it('filters the list by name or email', async () => {
    const user = userEvent.setup();
    wrap(<PmAccessAdmin apiService={makeApi()} />);
    await screen.findByLabelText('Access level for Ada Admin');

    await user.type(screen.getByLabelText('Find a person'), 'rae@');

    expect(screen.getByLabelText('Access level for Rae Reader')).toBeInTheDocument();
    expect(screen.queryByLabelText('Access level for Ada Admin')).not.toBeInTheDocument();
  });
});

describe('PmMembers: labelled controls and access', () => {
  const members = [
    { userId: 1, name: 'Ada Admin', email: 'ada@x.com', role: 'ProjectAdmin', moduleReadOnly: false },
    { userId: 2, name: 'Rae Reader', email: 'rae@x.com', role: 'Developer', moduleReadOnly: true },
    { userId: 3, name: 'Vic Viewer', email: 'vic@x.com', role: 'Viewer', moduleReadOnly: false },
  ];
  const makeApi = () => ({
    getPmMembers: vi.fn().mockResolvedValue(members),
    getPmUsers: vi.fn().mockResolvedValue([{ id: 9, name: 'Newcomer', email: 'new@x.com' }]),
    setPmMember: vi.fn().mockResolvedValue(null),
    removePmMember: vi.fn().mockResolvedValue(null),
  });
  const project = { id: 3, name: 'Khoi', myRole: 'ProjectAdmin', actualRole: 'ProjectAdmin' };

  it('labels the add-person controls - a person list is never mistaken for a project list', async () => {
    wrap(<PmMembers apiService={makeApi()} project={project} onChanged={vi.fn()} />);

    const form = await screen.findByRole('region', { name: 'Add people' });
    expect(within(form).getByRole('heading')).toHaveTextContent('Add people to Khoi');
    const person = within(form).getByLabelText('Person');
    expect(within(person).getByRole('option', { name: 'Choose a person…' })).toBeInTheDocument();
    expect(within(person).getByRole('option', { name: /Newcomer/ })).toBeInTheDocument();
    expect(within(form).getByLabelText('Role')).toBeInTheDocument();
    expect(within(form).getByRole('button', { name: 'Add to project' })).toBeDisabled();
  });

  it('adds the chosen person with the chosen role', async () => {
    const api = makeApi();
    const user = userEvent.setup();
    wrap(<PmMembers apiService={api} project={project} onChanged={vi.fn()} />);
    await user.selectOptions(await screen.findByLabelText('Person'), '9');
    await user.selectOptions(screen.getByLabelText('Role'), 'Tester');

    await user.click(screen.getByRole('button', { name: 'Add to project' }));

    await waitFor(() => expect(api.setPmMember).toHaveBeenCalledWith(3, 9, 'Tester'));
  });

  it('shows what each person can actually do, including a read-only level set by a SuperAdmin', async () => {
    wrap(<PmMembers apiService={makeApi()} project={project} onChanged={vi.fn()} currentUserId={1} />);
    await screen.findByText('Ada Admin');

    const rows = screen.getAllByRole('listitem').filter((li) => li.textContent.includes('@x.com'));
    expect(within(rows[0]).getByText('Full control')).toBeInTheDocument();
    expect(within(rows[0]).getByText('(you)')).toBeInTheDocument();
    expect(within(rows[1]).getByText('Read only')).toBeInTheDocument();   // a Developer, but read-only at module level
    expect(within(rows[2]).getByText('Read only')).toBeInTheDocument();   // a Viewer
  });

  it('has a "what can each role do" panel that names the viewer\'s own role', async () => {
    wrap(<PmMembers apiService={makeApi()} project={project} onChanged={vi.fn()} />);
    await screen.findByText('Ada Admin');

    expect(screen.getByText('What can each role do?')).toBeInTheDocument();
    expect(screen.getByRole('columnheader', { name: 'Project Admin' })).toHaveClass('bg-blue-50');
  });

  it('greys out member management for someone who cannot manage, with the reason', async () => {
    wrap(<PmMembers apiService={makeApi()} project={{ ...project, myRole: 'Viewer', actualRole: 'ProjectAdmin' }} onChanged={vi.fn()} />);
    await screen.findByText('Ada Admin');

    expect(screen.getByLabelText('Person')).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Add to project' })).toBeDisabled();
    expect(screen.getByLabelText('Select everyone')).toBeDisabled();
    expect(screen.getByLabelText('Role for Ada Admin')).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Remove Ada Admin' })).toBeDisabled();
    expect(screen.getByText(/can't manage members/i)).toBeInTheDocument();
  });
});

describe('ImportMembersModal access level', () => {
  const makeApi = () => ({
    importPmMembers: vi.fn().mockResolvedValue({ dryRun: true, added: 0, updated: 0, skipped: 0, moduleAccessGranted: 0, rows: [] }),
    getPmMembers: vi.fn().mockResolvedValue([]),
  });

  it('lets a SuperAdmin choose read only for the people being enabled', async () => {
    const api = makeApi();
    const user = userEvent.setup();
    wrap(<ImportMembersModal apiService={api} project={{ id: 3, name: 'Khoi' }} canGrantAccess onImported={vi.fn()} onClose={vi.fn()} />);
    await user.type(screen.getByLabelText('People'), 'pat@x.com');
    const group = screen.getByRole('radiogroup', { name: /access level/i });
    expect(within(group).getByLabelText('Read & write')).toBeDisabled();       // nothing to choose until access is being granted

    await user.click(screen.getByLabelText(/enable Project Management access/i));
    await user.click(within(group).getByLabelText('Read only'));
    await user.click(screen.getByRole('button', { name: 'Preview' }));

    await waitFor(() => expect(api.importPmMembers).toHaveBeenCalledWith(3, expect.objectContaining({ grantModuleAccess: true, readOnlyAccess: true })));
  });

  it('defaults to read & write, and never sends read-only without a grant', async () => {
    const api = makeApi();
    const user = userEvent.setup();
    wrap(<ImportMembersModal apiService={api} project={{ id: 3, name: 'Khoi' }} canGrantAccess onImported={vi.fn()} onClose={vi.fn()} />);
    await user.type(screen.getByLabelText('People'), 'pat@x.com');

    await user.click(screen.getByRole('button', { name: 'Preview' }));

    await waitFor(() => expect(api.importPmMembers).toHaveBeenCalledWith(3, expect.objectContaining({ grantModuleAccess: false, readOnlyAccess: false })));
  });
});

describe('PmProjectNav', () => {
  const projects = [
    { id: 1, name: 'Alpha', myRole: 'Developer', openItems: 2, readOnly: true },
    { id: 2, name: 'Beta', myRole: 'ProjectAdmin', openItems: 5, readOnly: false },
  ];

  it('marks the current project, and flags a read-only one', () => {
    wrap(<PmProjectNav projects={projects} route={{ kind: 'project', projectId: 2, tab: 'board' }} canAdminAccess={false} onOpenProject={vi.fn()} onOpenSection={vi.fn()} />);

    expect(screen.getByRole('button', { name: /Beta/ })).toHaveAttribute('aria-current', 'page');
    expect(screen.getByRole('button', { name: /Alpha/ })).not.toHaveAttribute('aria-current');
    expect(screen.getByLabelText('Read only')).toBeInTheDocument();
    expect(screen.getByText(/Project Admin · 5 open/)).toBeInTheDocument();
  });

  it('marks a workspace section as current instead of any project', () => {
    wrap(<PmProjectNav projects={projects} route={{ kind: 'mywork' }} canAdminAccess onOpenProject={vi.fn()} onOpenSection={vi.fn()} />);
    expect(screen.getByRole('button', { name: 'My work' })).toHaveAttribute('aria-current', 'page');
    expect(screen.getByRole('button', { name: /Beta/ })).not.toHaveAttribute('aria-current');
    expect(screen.getByRole('button', { name: 'Module access' })).toBeInTheDocument();
  });

  it('reports clicks', async () => {
    const onOpenProject = vi.fn();
    const onOpenSection = vi.fn();
    const user = userEvent.setup();
    wrap(<PmProjectNav projects={projects} route={{ kind: 'mywork' }} canAdminAccess onOpenProject={onOpenProject} onOpenSection={onOpenSection} />);

    await user.click(screen.getByRole('button', { name: /Alpha/ }));
    await user.click(screen.getByRole('button', { name: 'Module access' }));

    expect(onOpenProject).toHaveBeenCalledWith(1);
    expect(onOpenSection).toHaveBeenCalledWith('access');
  });
});
