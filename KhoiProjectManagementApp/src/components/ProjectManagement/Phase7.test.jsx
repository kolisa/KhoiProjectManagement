import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';
import { ToastProvider } from '../../contexts/ToastContext';
import { ConfirmProvider } from '../../contexts/ConfirmContext';
import { parseMemberRows } from './pmImport';
import ImportMembersModal from './ImportMembersModal';
import PmOverview from './PmOverview';
import PmMyWork from './PmMyWork';
import PmMembers from './PmMembers';
import WorkItemCollab from './WorkItemCollab';
import { LabelChips, RollupBar } from './pmWidgets';

const wrap = (ui) => render(<ToastProvider><ConfirmProvider>{ui}</ConfirmProvider></ToastProvider>);

describe('parseMemberRows', () => {
  it('reads comma, semicolon and tab separated lines, with quotes and a header row', () => {
    const rows = parseMemberRows('Email,Role\r\ndana@x.com, Scrum Master\nsam@x.com;dev\n"pat@x.com"\t"QA"\n\n  lee@x.com  ');
    expect(rows).toEqual([
      { email: 'dana@x.com', role: 'Scrum Master' },
      { email: 'sam@x.com', role: 'dev' },
      { email: 'pat@x.com', role: 'QA' },
      { email: 'lee@x.com', role: '' },
    ]);
  });

  it('keeps a first line that is a real address, and copes with empty input', () => {
    expect(parseMemberRows('dana@x.com,Developer')).toHaveLength(1);
    expect(parseMemberRows('')).toEqual([]);
    expect(parseMemberRows(null)).toEqual([]);
  });

  it('keeps a quoted delimiter inside a cell', () => {
    expect(parseMemberRows('"a,b@x.com",Developer')[0].email).toBe('a,b@x.com');
  });
});

describe('ImportMembersModal', () => {
  const previewResult = {
    dryRun: true, added: 1, updated: 0, skipped: 1, moduleAccessGranted: 0,
    rows: [
      { row: 1, email: 'dana@x.com', role: 'ScrumMaster', outcome: 'Added', message: 'Added to the project.', applied: true },
      { row: 2, email: 'ghost@x.com', role: 'Developer', outcome: 'NotFound', message: 'No account with this email.', applied: false },
    ],
  };
  const makeApi = () => ({
    importPmMembers: vi.fn().mockImplementation((_id, payload) => Promise.resolve(payload.dryRun ? previewResult : { ...previewResult, dryRun: false })),
    getPmMembers: vi.fn().mockResolvedValue([{ userId: 5, name: 'Sam', email: 'sam@x.com', role: 'Tester' }]),
  });
  const renderModal = (api, extra = {}) => wrap(
    <ImportMembersModal apiService={api} project={{ id: 3, name: 'Khoi' }} otherProjects={[{ id: 9, name: 'Other' }]}
      canGrantAccess={false} onImported={vi.fn()} onClose={vi.fn()} {...extra} />
  );

  it('previews first and imports only after the preview', async () => {
    const api = makeApi();
    const user = userEvent.setup();
    renderModal(api);
    const importButton = () => screen.getByRole('button', { name: /^Import/ });
    expect(importButton()).toBeDisabled();

    await user.type(screen.getByLabelText('People'), 'dana@x.com, Scrum Master');
    await user.click(screen.getByRole('button', { name: 'Preview' }));

    expect(await screen.findByRole('region', { name: 'Import preview' })).toBeInTheDocument();
    expect(api.importPmMembers).toHaveBeenLastCalledWith(3, expect.objectContaining({ dryRun: true, rows: [{ email: 'dana@x.com', role: 'Scrum Master' }] }));
    expect(screen.getByText('No account')).toBeInTheDocument();
    expect(api.importPmMembers).toHaveBeenCalledTimes(1);

    await user.click(importButton());

    await waitFor(() => expect(api.importPmMembers).toHaveBeenLastCalledWith(3, expect.objectContaining({ dryRun: false })));
  });

  it('applies the default role to lines without one, and invalidates the preview when the text changes', async () => {
    const api = makeApi();
    const user = userEvent.setup();
    renderModal(api);
    await user.selectOptions(screen.getByLabelText('Role when a line has none'), 'Tester');
    await user.type(screen.getByLabelText('People'), 'sam@x.com');
    await user.click(screen.getByRole('button', { name: 'Preview' }));
    await screen.findByRole('region', { name: 'Import preview' });
    expect(api.importPmMembers.mock.calls[0][1].rows).toEqual([{ email: 'sam@x.com', role: 'Tester' }]);

    await user.type(screen.getByLabelText('People'), '\nmore@x.com');

    expect(screen.queryByRole('region', { name: 'Import preview' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: /^Import/ })).toBeDisabled();
  });

  it('shows the module-access options only to people allowed to grant it', async () => {
    const user = userEvent.setup();
    const { unmount } = renderModal(makeApi(), { canGrantAccess: false });
    expect(screen.queryByText(/enable Project Management access/i)).not.toBeInTheDocument();
    unmount();

    const api = makeApi();
    renderModal(api, { canGrantAccess: true });
    await user.click(screen.getByLabelText(/enable Project Management access/i));
    await user.click(screen.getByLabelText(/confine them to this module/i));
    await user.type(screen.getByLabelText('People'), 'pat@x.com');
    await user.click(screen.getByRole('button', { name: 'Preview' }));

    await waitFor(() => expect(api.importPmMembers).toHaveBeenCalledWith(3, expect.objectContaining({ grantModuleAccess: true, moduleOnly: true })));
  });

  it('copies another project\'s roster into the box', async () => {
    const api = makeApi();
    const user = userEvent.setup();
    renderModal(api);

    await user.selectOptions(screen.getByLabelText('Copy members from another project'), '9');

    await waitFor(() => expect(screen.getByLabelText('People')).toHaveValue('sam@x.com,Tester'));
  });

  it('shows a server refusal and stays open', async () => {
    const api = makeApi();
    api.importPmMembers.mockRejectedValue(new Error('This import would leave the project without a Project Admin.'));
    const user = userEvent.setup();
    renderModal(api);
    await user.type(screen.getByLabelText('People'), 'a@x.com');
    await user.click(screen.getByRole('button', { name: 'Preview' }));
    expect(await screen.findByRole('alert')).toHaveTextContent(/without a Project Admin/);
  });
});

describe('PmOverview', () => {
  const overview = {
    projectId: 1, projectName: 'Khoi', openPoints: 21, overdueCount: 2, blockedCount: 1, unassignedCount: 4,
    statusCounts: { todo: 5, 'in-progress': 3, blocked: 1, completed: 6 },
    activeSprint: { id: 7, name: 'Sprint 7', goal: 'Ship login', startDate: '2026-11-02T00:00:00Z', endDate: '2026-11-15T00:00:00Z', daysLeft: 5, totalPoints: 20, donePoints: 10, itemsTotal: 8, itemsDone: 4 },
    dueSoon: [{ id: 4, title: 'Fix CSS', type: 'Bug', status: 'todo', priority: 'high', dueDate: '2026-11-12T00:00:00Z', isOverdue: false }],
    myOpenItems: [], recentActivity: [{ when: '2026-11-10T08:00:00Z', actorName: 'Dana', text: 'commented on "Login"' }],
  };

  it('summarises sprint, risk, status mix and activity', async () => {
    wrap(<PmOverview apiService={{ getPmOverview: vi.fn().mockResolvedValue(overview) }} project={{ id: 1 }} />);

    expect(await screen.findByText('Sprint 7')).toBeInTheDocument();
    expect(screen.getByText('Ship login')).toBeInTheDocument();
    expect(screen.getByText(/10 of 20 points done · 4 of 8 items/)).toBeInTheDocument();
    expect(screen.getByRole('progressbar', { name: 'Sprint progress' })).toHaveAttribute('aria-valuenow', '50');
    expect(screen.getByText('21')).toBeInTheDocument();
    expect(screen.getByText(/Fix CSS/)).toBeInTheDocument();
    expect(screen.getByText('Nothing open is assigned to you here.')).toBeInTheDocument();
    expect(screen.getByText(/commented on "Login"/)).toBeInTheDocument();
    expect(screen.getByRole('img', { name: /5 To do, 3 In progress, 1 Blocked, 6 Done/ })).toBeInTheDocument();
  });

  it('prompts to start a sprint when none is running, and offers a retry on failure', async () => {
    const { unmount } = wrap(<PmOverview apiService={{ getPmOverview: vi.fn().mockResolvedValue({ ...overview, activeSprint: null }) }} project={{ id: 1 }} />);
    expect(await screen.findByText(/No sprint is running/)).toBeInTheDocument();
    unmount();

    wrap(<PmOverview apiService={{ getPmOverview: vi.fn().mockRejectedValue(new Error('boom')) }} project={{ id: 1 }} />);
    expect(await screen.findByText(/boom/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /try again/i })).toBeInTheDocument();
  });
});

describe('PmMyWork', () => {
  const items = [
    { id: 1, projectId: 1, projectName: 'Alpha', title: 'Late thing', type: 'Task', status: 'todo', priority: 'high', storyPoints: 3, dueDate: '2026-11-01T00:00:00Z', isOverdue: true, sprintName: 'S1' },
    { id: 2, projectId: 2, projectName: 'Beta', title: 'Fine thing', type: 'Bug', status: 'in-progress', priority: 'low', storyPoints: 2, dueDate: '2026-12-01T00:00:00Z', isOverdue: false },
  ];

  it('lists work across projects, filters by project, and jumps to a project', async () => {
    const onOpenProject = vi.fn();
    const user = userEvent.setup();
    wrap(<PmMyWork apiService={{ getMyWork: vi.fn().mockResolvedValue(items) }} onOpenProject={onOpenProject} />);

    expect(await screen.findByText('Late thing')).toBeInTheDocument();
    expect(screen.getByText(/2 open items · 5 points/)).toHaveTextContent('1 overdue');

    await user.selectOptions(screen.getByLabelText('Filter by project'), '2');
    expect(screen.queryByText('Late thing')).not.toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: /Open Beta for Fine thing/ }));
    expect(onOpenProject).toHaveBeenCalledWith(2);
  });

  it('says so when nothing is assigned', async () => {
    wrap(<PmMyWork apiService={{ getMyWork: vi.fn().mockResolvedValue([]) }} onOpenProject={vi.fn()} />);
    expect(await screen.findByText(/Nothing open is assigned to you/)).toBeInTheDocument();
  });
});

describe('Labels and history on a work item', () => {
  const item = { id: 7, projectId: 5, title: 'Login', labels: [{ id: 1, name: 'ui', color: '#3B82F6' }] };
  const makeApi = () => ({
    getWorkItemComments: vi.fn().mockResolvedValue([]),
    getWorkItemChecklist: vi.fn().mockResolvedValue([]),
    getWorkItemLinks: vi.fn().mockResolvedValue([]),
    getWorkItemLookup: vi.fn().mockResolvedValue([]),
    getWorkItemAttachments: vi.fn().mockResolvedValue([]),
    getProjectLabels: vi.fn().mockResolvedValue([{ id: 1, name: 'ui' }, { id: 2, name: 'api' }]),
    setWorkItemLabels: vi.fn().mockImplementation((_id, names) => Promise.resolve(names.map((n, i) => ({ id: i + 10, name: n, color: '#111' })))),
    getWorkItemActivity: vi.fn().mockResolvedValue([
      { when: '2026-11-10T08:00:00Z', actorName: 'Sam', kind: 'status', text: 'moved this from To do to In progress' },
      { when: '2026-11-09T08:00:00Z', actorName: 'Dana', kind: 'activity', text: 'created this item' },
    ]),
  });
  const renderCollab = (api, edit = true, onChanged = vi.fn()) => wrap(
    <WorkItemCollab apiService={api} item={item} items={[item]} members={[]} can={{ collaborate: edit, edit }} onChanged={onChanged} />
  );

  it('adds a label, saving the whole set, and tells the parent to refresh', async () => {
    const api = makeApi();
    const onChanged = vi.fn();
    const user = userEvent.setup();
    renderCollab(api, true, onChanged);

    await user.type(screen.getByLabelText('Add a label'), 'Customer Facing{Enter}');

    await waitFor(() => expect(api.setWorkItemLabels).toHaveBeenCalledWith(7, ['ui', 'Customer Facing']));
    expect(await screen.findByText('Customer Facing')).toBeInTheDocument();
    expect(onChanged).toHaveBeenCalled();
  });

  it('removes a label, and ignores a duplicate (any case)', async () => {
    const api = makeApi();
    const user = userEvent.setup();
    renderCollab(api);

    await user.type(screen.getByLabelText('Add a label'), 'UI{Enter}');
    expect(api.setWorkItemLabels).not.toHaveBeenCalled();

    await user.click(screen.getByRole('button', { name: 'Remove label ui' }));
    await waitFor(() => expect(api.setWorkItemLabels).toHaveBeenCalledWith(7, []));
  });

  it('is read-only for roles that cannot edit', async () => {
    renderCollab(makeApi(), false);
    expect(screen.getByText('ui')).toBeInTheDocument();
    expect(screen.queryByLabelText('Add a label')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Remove label/ })).not.toBeInTheDocument();
  });

  it('loads the history only when its tab is opened', async () => {
    const api = makeApi();
    const user = userEvent.setup();
    renderCollab(api);
    await waitFor(() => expect(api.getWorkItemComments).toHaveBeenCalled());
    expect(api.getWorkItemActivity).not.toHaveBeenCalled();

    await user.click(screen.getByRole('tab', { name: /history/i }));

    expect(await screen.findByText(/moved this from To do to In progress/)).toBeInTheDocument();
    expect(screen.getByText(/created this item/)).toBeInTheDocument();
    expect(api.getWorkItemActivity).toHaveBeenCalledTimes(1);
  });
});

describe('PmMembers bulk actions', () => {
  const members = [
    { userId: 1, name: 'Ada Admin', email: 'ada@x.com', role: 'ProjectAdmin' },
    { userId: 2, name: 'Dana Dev', email: 'dana@x.com', role: 'Developer' },
    { userId: 3, name: 'Sam Test', email: 'sam@x.com', role: 'Tester' },
  ];
  const makeApi = () => ({
    getPmMembers: vi.fn().mockResolvedValue(members),
    getPmUsers: vi.fn().mockResolvedValue([]),
    setPmMember: vi.fn().mockResolvedValue(null),
    removePmMember: vi.fn().mockResolvedValue(null),
  });
  const renderMembers = (api) => wrap(<PmMembers apiService={api} project={{ id: 3, name: 'Khoi', myRole: 'ProjectAdmin' }} onChanged={vi.fn()} />);

  it('sets one role for everyone selected', async () => {
    const api = makeApi();
    const user = userEvent.setup();
    renderMembers(api);
    await user.click(await screen.findByLabelText('Select Dana Dev'));
    await user.click(screen.getByLabelText('Select Sam Test'));
    await user.selectOptions(screen.getByLabelText('Role for selected people'), 'ScrumMaster');

    await user.click(screen.getByRole('button', { name: 'Set role' }));

    await waitFor(() => expect(api.setPmMember).toHaveBeenCalledTimes(2));
    expect(api.setPmMember).toHaveBeenCalledWith(3, 2, 'ScrumMaster');
    expect(api.setPmMember).toHaveBeenCalledWith(3, 3, 'ScrumMaster');
  });

  it('reports the people a bulk change could not be applied to, without undoing the rest', async () => {
    const api = makeApi();
    api.setPmMember.mockImplementation((_p, id) => (id === 1 ? Promise.reject(new Error('keep one admin')) : Promise.resolve(null)));
    const user = userEvent.setup();
    renderMembers(api);
    await user.click(await screen.findByLabelText('Select everyone'));
    await user.click(screen.getByRole('button', { name: 'Set role' }));

    expect(await screen.findByText(/Ada Admin: keep one admin/)).toBeInTheDocument();
    expect(api.setPmMember).toHaveBeenCalledTimes(3);
  });

  it('asks before removing the selection', async () => {
    const api = makeApi();
    const user = userEvent.setup();
    renderMembers(api);
    await user.click(await screen.findByLabelText('Select Sam Test'));

    await user.click(screen.getByRole('button', { name: 'Remove selected' }));
    expect(api.removePmMember).not.toHaveBeenCalled();
    await user.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Remove' }));

    await waitFor(() => expect(api.removePmMember).toHaveBeenCalledWith(3, 3));
  });

  it('offers import only to people who can manage members', async () => {
    const api = makeApi();
    wrap(<PmMembers apiService={api} project={{ id: 3, name: 'Khoi', myRole: 'Developer' }} onChanged={vi.fn()} />);
    await screen.findByText('Dana Dev');
    expect(screen.queryByRole('button', { name: /Import members/ })).not.toBeInTheDocument();
    expect(screen.queryByLabelText('Select Dana Dev')).not.toBeInTheDocument();
  });
});

describe('shared widgets', () => {
  it('RollupBar shows nothing without children and a percentage with them', () => {
    const { container, rerender } = render(<RollupBar done={0} total={0} />);
    expect(container).toBeEmptyDOMElement();
    rerender(<RollupBar done={3} total={4} />);
    expect(screen.getByRole('progressbar')).toHaveAttribute('aria-valuenow', '75');
    expect(screen.getByText('3/4')).toBeInTheDocument();
  });

  it('LabelChips caps the list and counts the rest', () => {
    render(<LabelChips max={2} labels={[1, 2, 3, 4].map((i) => ({ id: i, name: `l${i}`, color: '#000' }))} />);
    expect(screen.getByText('l2')).toBeInTheDocument();
    expect(screen.queryByText('l3')).not.toBeInTheDocument();
    expect(screen.getByText('+2')).toBeInTheDocument();
  });
});
