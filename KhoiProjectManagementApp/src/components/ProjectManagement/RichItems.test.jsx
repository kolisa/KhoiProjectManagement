import { render, screen, waitFor, fireEvent, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';
import { ToastProvider } from '../../contexts/ToastContext';
import { ConfirmProvider } from '../../contexts/ConfirmContext';
import RichTextEditor from './RichTextEditor';
import SubtasksPanel from './SubtasksPanel';
import EffortEditor from './EffortEditor';
import SprintEffort from './SprintEffort';
import WorkItemModal from './WorkItemModal';

const wrap = (ui) => render(<ToastProvider><ConfirmProvider>{ui}</ConfirmProvider></ToastProvider>);
const members = [{ userId: 1, name: 'Ada Lovelace' }, { userId: 2, name: 'Ben Carter' }];

describe('RichTextEditor', () => {
  it('shows legacy plain text as paragraphs and reports sanitised html when edited', async () => {
    const onChange = vi.fn();
    render(<RichTextEditor id="d" label="Description" value={'First line\n\nSecond'} onChange={onChange} />);
    const box = screen.getByRole('textbox', { name: 'Description' });
    expect(box.innerHTML).toBe('<p>First line</p><p>Second</p>');

    box.innerHTML = '<p>Hello <script>x</script><b onclick="y()">there</b></p>';
    fireEvent.input(box);

    expect(onChange).toHaveBeenLastCalledWith('<p>Hello <b>there</b></p>');
  });

  it('tidies spelling and grammar, says what changed, and can undo it', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<RichTextEditor id="d" label="Description" value="<p>i recieve teh data</p>" onChange={onChange} />);
    const box = screen.getByRole('textbox', { name: 'Description' });

    await user.click(screen.getByRole('button', { name: /tidy spelling/i }));

    expect(box.innerHTML).toBe('<p>I receive the data</p>');
    expect(screen.getByRole('status')).toHaveTextContent(/misspelled words/i);
    expect(onChange).toHaveBeenLastCalledWith('<p>I receive the data</p>');

    await user.click(screen.getByRole('button', { name: /undo/i }));
    expect(box.innerHTML).toBe('<p>i recieve teh data</p>');
  });

  it('says so when there is nothing to fix', async () => {
    const user = userEvent.setup();
    render(<RichTextEditor id="d" label="Description" value="<p>All good.</p>" onChange={vi.fn()} />);
    await user.click(screen.getByRole('button', { name: /tidy spelling/i }));
    expect(screen.getByRole('status')).toHaveTextContent(/nothing to fix/i);
  });

  it('is read-only, with the reason, when the person cannot edit', () => {
    render(<RichTextEditor id="d" label="Description" value="<p>Locked</p>" onChange={vi.fn()} disabled reason="Your access is read-only." />);
    const box = screen.getByRole('textbox', { name: 'Description' });
    expect(box).toHaveAttribute('contenteditable', 'false');
    expect(box).toHaveTextContent('Locked');
    expect(screen.queryByRole('toolbar')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /tidy/i })).not.toBeInTheDocument();
  });

  it('turns on the browser spell-checker while editing', () => {
    render(<RichTextEditor id="d" label="Description" value="" onChange={vi.fn()} />);
    expect(screen.getByRole('textbox', { name: 'Description' })).toHaveAttribute('spellcheck', 'true');
  });
});

describe('SubtasksPanel', () => {
  const item = { id: 10, type: 'Story', priority: 'high', assignedToId: 1 };
  const items = [
    item,
    { id: 11, parentId: 10, title: 'Wire the API', status: 'completed', assignedToId: 2 },
    { id: 12, parentId: 10, title: 'Write tests', status: 'todo', assignedToId: null },
    { id: 99, parentId: 50, title: 'Someone else\'s subtask', status: 'todo' },
  ];
  const makeApi = () => ({ createWorkItem: vi.fn().mockResolvedValue({}), moveWorkItem: vi.fn().mockResolvedValue({}) });
  const base = { project: { id: 5 }, item, items, members, description: '', canCreate: true, onChanged: vi.fn() };

  it('lists only this item\'s subtasks with progress', () => {
    wrap(<SubtasksPanel apiService={makeApi()} {...base} />);
    expect(screen.getByText('1/2 done')).toBeInTheDocument();
    expect(screen.getByText('Wire the API')).toBeInTheDocument();
    expect(screen.queryByText(/someone else/i)).not.toBeInTheDocument();
  });

  it('adds a subtask on Enter, as a Subtask under the item, assigned to the chosen person', async () => {
    const api = makeApi();
    const user = userEvent.setup();
    wrap(<SubtasksPanel apiService={api} {...base} />);

    await user.click(screen.getByLabelText('Subtask assignee'));
    await user.click(screen.getByRole('option', { name: /Ben Carter/ }));
    await user.type(screen.getByLabelText('New subtask'), 'Deploy to staging{Enter}');

    await waitFor(() => expect(api.createWorkItem).toHaveBeenCalledWith(5, expect.objectContaining({
      title: 'Deploy to staging', type: 'Subtask', parentId: 10, assignedToId: 2, status: 'todo',
    })));
  });

  it('ticks a subtask done', async () => {
    const api = makeApi();
    const user = userEvent.setup();
    wrap(<SubtasksPanel apiService={api} {...base} />);
    await user.click(screen.getByRole('checkbox', { name: /mark write tests done/i }));
    expect(api.moveWorkItem).toHaveBeenCalledWith(12, 'completed', null);
  });

  it('suggests subtasks from the description and adds the ones left ticked', async () => {
    const api = makeApi();
    const user = userEvent.setup();
    wrap(<SubtasksPanel apiService={api} {...base}
      description="<ul><li>Create the login form</li><li>Write tests</li><li>Add validation</li></ul>" />);

    await user.click(screen.getByRole('button', { name: /suggest from description/i }));
    const panel = screen.getByRole('region', { name: 'Suggested subtasks' });
    // "Write tests" already exists, so it is not suggested again.
    expect(within(panel).queryByText('Write tests')).not.toBeInTheDocument();
    await user.click(within(panel).getByRole('checkbox', { name: 'Add validation' }));   // untick one
    await user.click(within(panel).getByRole('button', { name: /add 1 subtask/i }));

    await waitFor(() => expect(api.createWorkItem).toHaveBeenCalledTimes(1));
    expect(api.createWorkItem).toHaveBeenCalledWith(5, expect.objectContaining({ title: 'Create the login form', type: 'Subtask', parentId: 10 }));
  });

  it('explains when there is nothing to suggest', async () => {
    const user = userEvent.setup();
    wrap(<SubtasksPanel apiService={makeApi()} {...base} description="<p>Short.</p>" />);
    await user.click(screen.getByRole('button', { name: /suggest from description/i }));
    expect(screen.getByText(/nothing to suggest yet/i)).toBeInTheDocument();
  });

  it('is greyed out, with the reason, for a role that cannot create', () => {
    wrap(<SubtasksPanel apiService={makeApi()} {...base} canCreate={false} reason="Your role (Viewer) can't create work items." />);
    expect(screen.getByLabelText('New subtask')).toBeDisabled();
    expect(screen.getByRole('button', { name: /suggest from description/i })).toBeDisabled();
    expect(screen.getByText(/can't create work items/)).toBeInTheDocument();
  });

  it('does not let a subtask have subtasks, and waits for a save before a new item can', () => {
    const { unmount } = wrap(<SubtasksPanel apiService={makeApi()} {...base} item={{ ...item, type: 'Subtask' }} />);
    expect(screen.getByLabelText('New subtask')).toBeDisabled();
    expect(screen.getByText(/cannot have subtasks of its own/i)).toBeInTheDocument();
    unmount();
    wrap(<SubtasksPanel apiService={makeApi()} {...base} item={null} />);
    expect(screen.getByText(/save the item first/i)).toBeInTheDocument();
  });
});

describe('EffortEditor', () => {
  const item = { id: 10 };
  const effort = {
    itemId: 10, total: 47,
    people: [{ userId: 1, userName: 'Ada Lovelace', total: 47, cells: [
      { category: 'AnalysisDesign', points: 12 }, { category: 'DevEffort', points: 30 }, { category: 'DevTesting', points: 3 },
      { category: 'Deployment', points: 1 }, { category: 'Demo', points: 0.5 }, { category: 'Monitoring', points: 0.5 }, { category: 'Training', points: null },
    ] }],
  };
  const makeApi = () => ({ getWorkItemEffort: vi.fn().mockResolvedValue(effort), saveWorkItemEffort: vi.fn().mockImplementation(async (id, people) => ({ itemId: id, total: 0, people: people.map((p) => ({ userId: p.userId, userName: 'x', total: 0, cells: p.cells.map((c) => ({ category: c.category, points: c.points })) })) })) });

  it('shows each person with one box per category, N/A kept apart from 0', async () => {
    wrap(<EffortEditor apiService={makeApi()} item={item} members={members} canEdit />);
    const group = await screen.findByRole('group', { name: 'Effort for Ada Lovelace' });

    expect(within(group).getByLabelText('Analysis & design')).toHaveValue(12);
    expect(within(group).getByLabelText('Dev effort')).toHaveValue(30);
    expect(within(group).getByLabelText('Demo')).toHaveValue(0.5);
    expect(within(group).getByLabelText('Training')).toBeDisabled();            // N/A
    expect(within(group).getByLabelText(/Training is not applicable/)).toBeChecked();
    expect(screen.getAllByText(/47 points/)).toHaveLength(2);   // the person's total and the item's
  });

  it('saves exactly what was entered: numbers as numbers, N/A as null, blanks left out', async () => {
    const api = makeApi();
    const user = userEvent.setup();
    wrap(<EffortEditor apiService={api} item={item} members={members} canEdit />);
    await screen.findByRole('group', { name: 'Effort for Ada Lovelace' });

    await user.click(screen.getByLabelText('Add a person to the effort'));
    await user.click(screen.getByRole('option', { name: /Ben Carter/ }));
    const ben = screen.getByRole('group', { name: 'Effort for Ben Carter' });
    await user.type(within(ben).getByLabelText('Dev effort'), '8');
    await user.click(within(ben).getByLabelText(/Training is not applicable/));
    await user.click(screen.getByRole('button', { name: 'Save effort' }));

    await waitFor(() => expect(api.saveWorkItemEffort).toHaveBeenCalled());
    const [, people] = api.saveWorkItemEffort.mock.calls[0];
    expect(people.find((p) => p.userId === 2).cells).toEqual([{ category: 'DevEffort', points: 8 }, { category: 'Training', points: null }]);
    expect(people.find((p) => p.userId === 1).cells).toContainEqual({ category: 'Training', points: null });
  });

  it('will not save a bad number', async () => {
    const user = userEvent.setup();
    wrap(<EffortEditor apiService={makeApi()} item={item} members={members} canEdit />);
    const group = await screen.findByRole('group', { name: 'Effort for Ada Lovelace' });
    const box = within(group).getByLabelText('Demo');
    await user.clear(box);
    await user.type(box, '-3');
    expect(screen.getByRole('button', { name: 'Save effort' })).toBeDisabled();
    expect(screen.getByRole('alert')).toHaveTextContent(/between 0 and 1000/);
  });

  it('is greyed out, with the reason, for a read-only person', async () => {
    wrap(<EffortEditor apiService={makeApi()} item={item} members={members} canEdit={false} reason="Your access is read-only." />);
    const group = await screen.findByRole('group', { name: 'Effort for Ada Lovelace' });
    expect(within(group).getByLabelText('Dev effort')).toBeDisabled();
    expect(screen.getByLabelText('Add a person to the effort')).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Save effort' })).toBeDisabled();
    expect(screen.getByText('Your access is read-only.')).toBeInTheDocument();
  });
});

describe('SprintEffort', () => {
  const sprints = [{ id: 3, name: 'Sprint 3', state: 'Active' }, { id: 2, name: 'Sprint 2', state: 'Completed' }];
  const data = {
    sprintId: 3, name: 'Sprint 3', total: 53, done: 42,
    unallocated: [{ itemId: 12, title: 'Nobody yet', type: 'Task', status: 'todo', total: 0, cells: [] }],
    people: [
      { userId: 1, userName: 'Ada Lovelace', total: 45, done: 42,
        categories: [{ category: 'AnalysisDesign', points: 12 }, { category: 'DevEffort', points: 30 }, { category: 'DevTesting', points: 3 }],
        items: [
          { itemId: 10, title: 'Login', type: 'Story', status: 'completed', total: 42, cells: [{ category: 'AnalysisDesign', points: 12 }, { category: 'DevEffort', points: 30 }] },
          { itemId: 11, title: 'Wire API', type: 'Subtask', status: 'todo', parentTitle: 'Login', total: 3, cells: [{ category: 'DevTesting', points: 3 }, { category: 'Training', points: null }] },
        ] },
      { userId: 2, userName: 'Ben Carter', total: 8, done: 0, categories: [{ category: 'DevEffort', points: 8 }], items: [] },
    ],
  };

  it('shows points per person for the active sprint, then drills into items and categories', async () => {
    const api = { getSprintEffort: vi.fn().mockResolvedValue(data) };
    const user = userEvent.setup();
    wrap(<SprintEffort apiService={api} sprints={sprints} refreshKey={[]} />);

    expect(await screen.findByRole('button', { name: 'Ada Lovelace: 45 points' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Ben Carter: 8 points' })).toBeInTheDocument();
    expect(api.getSprintEffort).toHaveBeenCalledWith(3);          // the active sprint is the default
    expect(screen.getByText('42 of 53 done')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Ada Lovelace: 45 points' }));      // person -> categories + items
    expect(screen.getByText('Wire API')).toBeInTheDocument();
    expect(screen.getByText(/in Login/)).toBeInTheDocument();

    await user.click(screen.getByText('Wire API'));                                           // item -> categories
    expect(screen.getAllByText('N/A').length).toBeGreaterThan(0);
  });

  it('flags items nobody has allocated points to', async () => {
    wrap(<SprintEffort apiService={{ getSprintEffort: vi.fn().mockResolvedValue(data) }} sprints={sprints} refreshKey={[]} />);
    expect(await screen.findByRole('note')).toHaveTextContent('Nobody yet');
  });

  it('loads another sprint when picked', async () => {
    const api = { getSprintEffort: vi.fn().mockResolvedValue(data) };
    const user = userEvent.setup();
    wrap(<SprintEffort apiService={api} sprints={sprints} refreshKey={[]} />);
    await screen.findByRole('button', { name: 'Ada Lovelace: 45 points' });

    await user.selectOptions(screen.getByLabelText('Sprint'), '2');

    await waitFor(() => expect(api.getSprintEffort).toHaveBeenCalledWith(2));
  });

  it('invites you to create a sprint when there are none', () => {
    wrap(<SprintEffort apiService={{ getSprintEffort: vi.fn() }} sprints={[]} refreshKey={[]} />);
    expect(screen.getByText(/create a sprint/i)).toBeInTheDocument();
  });
});

describe('WorkItemModal (redesigned)', () => {
  const project = { id: 5, myRole: 'Developer', actualRole: 'Developer' };
  const item = { id: 10, title: 'Login', description: '<p>Build <strong>it</strong></p>', type: 'Story', status: 'todo', priority: 'medium', storyPoints: 5, assignedToId: 1, parentId: null, hasDueDate: false };
  const api = () => ({
    getWorkItemEffort: vi.fn().mockResolvedValue({ itemId: 10, total: 0, people: [] }),
    getWorkItemComments: vi.fn().mockResolvedValue([]), getWorkItemChecklist: vi.fn().mockResolvedValue([]),
    getWorkItemLinks: vi.fn().mockResolvedValue([]), getWorkItemAttachments: vi.fn().mockResolvedValue([]),
    getProjectLabels: vi.fn().mockResolvedValue([]), getWorkItemLookup: vi.fn().mockResolvedValue([]),
  });

  it('shows a rich-text description, subtasks, effort and details side by side', async () => {
    wrap(<WorkItemModal item={item} items={[item]} members={members} canEdit apiService={api()} role="Developer" project={project} onSave={vi.fn()} onClose={vi.fn()} />);

    expect(screen.getByRole('textbox', { name: 'Description' }).innerHTML).toBe('<p>Build <strong>it</strong></p>');
    expect(screen.getByRole('region', { name: 'Subtasks' })).toBeInTheDocument();
    expect(await screen.findByRole('region', { name: 'Effort' })).toBeInTheDocument();
    expect(screen.getByRole('complementary', { name: 'Details' })).toBeInTheDocument();
    expect(screen.getByLabelText('Status')).toBeInTheDocument();
  });

  it('saves the description as html', async () => {
    const onSave = vi.fn().mockResolvedValue({});
    const user = userEvent.setup();
    wrap(<WorkItemModal item={item} items={[item]} members={members} canEdit apiService={api()} role="Developer" project={project} onSave={onSave} onClose={vi.fn()} />);

    const box = screen.getByRole('textbox', { name: 'Description' });
    box.innerHTML = '<p>New <em>text</em></p>';
    fireEvent.input(box);
    await user.click(screen.getByRole('button', { name: 'Save' }));

    await waitFor(() => expect(onSave).toHaveBeenCalledWith(expect.objectContaining({ description: '<p>New <em>text</em></p>' })));
  });

  it('is read-only for a Viewer: description locked, no Save', async () => {
    const viewer = { ...project, myRole: 'Viewer', actualRole: 'Viewer' };
    wrap(<WorkItemModal item={item} items={[item]} members={members} canEdit={false} apiService={api()} role="Viewer" project={viewer} onSave={vi.fn()} onClose={vi.fn()} />);

    expect(screen.getByRole('textbox', { name: 'Description' })).toHaveAttribute('contenteditable', 'false');
    expect(screen.queryByRole('button', { name: 'Save' })).not.toBeInTheDocument();
    expect(screen.getByLabelText('New subtask')).toBeDisabled();
  });
});
