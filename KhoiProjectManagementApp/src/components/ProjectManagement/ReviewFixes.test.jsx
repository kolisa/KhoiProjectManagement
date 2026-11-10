import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';
import { ToastProvider } from '../../contexts/ToastContext';
import { ConfirmProvider } from '../../contexts/ConfirmContext';
import RichTextEditor from './RichTextEditor';
import PmBoard from './PmBoard';
import PmSprints from './PmSprints';
import PmBacklog from './PmBacklog';
import WorkItemModal from './WorkItemModal';

vi.mock('./PmCharts', () => ({ BurndownChart: () => <div />, VelocityChart: () => <div /> }));
vi.mock('./SprintEffort', () => ({ default: () => <div /> }));

const wrap = (ui) => render(<ToastProvider><ConfirmProvider>{ui}</ConfirmProvider></ToastProvider>);
const members = [{ userId: 1, name: 'Ada Lovelace', email: 'a@x.com' }];
const project = { id: 5, name: 'Khoi', myRole: 'ScrumMaster', actualRole: 'ScrumMaster' };
const sprint = (over) => ({ id: 1, projectId: 5, name: 'Sprint 1', goal: '', state: 'Active', startDate: '2026-11-01', endDate: '2099-11-14', totalPoints: 0, donePoints: 0, itemCount: 0, ...over });

describe('browser compatibility guard', () => {
  it('uses no regex lookbehind anywhere in the app source (a syntax error on Safari before 16.4 would blank the whole app)', () => {
    const files = import.meta.glob('/src/**/*.{js,jsx}', { query: '?raw', import: 'default', eager: true });
    const offenders = Object.entries(files)
      .filter(([path]) => !/\.test\.jsx?$/.test(path))
      .filter(([, text]) => /\(\?<[=!]/.test(text))
      .map(([path]) => path);
    expect(offenders).toEqual([]);
  });
});

describe('RichTextEditor undo', () => {
  it('offers Undo right after tidying, but not once you have typed again', async () => {
    const user = userEvent.setup();
    render(<RichTextEditor id="d" label="Description" value="<p>teh plan</p>" onChange={vi.fn()} />);
    await user.click(screen.getByRole('button', { name: /tidy spelling/i }));
    expect(screen.getByRole('button', { name: /undo/i })).toBeInTheDocument();

    fireEvent.input(screen.getByRole('textbox', { name: 'Description' }));      // the person types something

    expect(screen.queryByRole('button', { name: /undo/i })).not.toBeInTheDocument();   // undo would now throw their typing away
  });
});

describe('PmBoard when the project changes', () => {
  it('does not ask the new project for the old project\'s sprint', async () => {
    const api = {
      getSprints: vi.fn().mockImplementation(async (id) => (id === 5 ? [sprint({ id: 1 })] : [])),
      getBoard: vi.fn().mockResolvedValue({ projectId: 5, columns: [{ id: 1, name: 'To do', status: 'todo', count: 0, wipLimit: null, overWip: false, items: [] }] }),
      getWorkflow: vi.fn().mockResolvedValue(null),
    };
    const { rerender } = wrap(<PmBoard apiService={api} project={project} members={members} />);
    await waitFor(() => expect(api.getBoard).toHaveBeenCalledWith(5, '1'));

    rerender(<ToastProvider><ConfirmProvider><PmBoard apiService={api} project={{ ...project, id: 6 }} members={members} /></ConfirmProvider></ToastProvider>);

    await waitFor(() => expect(api.getBoard).toHaveBeenCalledWith(6, null));
    expect(api.getBoard).not.toHaveBeenCalledWith(6, '1');
  });
});

describe('PmSprints dragging', () => {
  const api = () => ({
    getSprints: vi.fn().mockResolvedValue([sprint({ id: 1 })]),
    getWorkItems: vi.fn().mockResolvedValue([{ id: 3, title: 'Gamma', type: 'Task', status: 'todo', sprintId: 1 }]),
    getVelocity: vi.fn().mockResolvedValue({ sprints: [] }),
    getBurndown: vi.fn().mockResolvedValue(null),
    assignWorkItemSprint: vi.fn().mockResolvedValue({}),
  });

  it('does nothing when an item is dropped on the sprint it is already in', async () => {
    const a = api();
    wrap(<PmSprints apiService={a} project={project} />);
    const row = (await screen.findByText('Gamma')).closest('li');
    const target = screen.getByRole('region', { name: 'Sprint 1' });

    fireEvent.dragStart(row);
    fireEvent.dragOver(target);
    fireEvent.drop(target);

    await new Promise((r) => setTimeout(r, 50));
    expect(a.assignWorkItemSprint).not.toHaveBeenCalled();
  });
});

describe('PmBacklog sprint picker', () => {
  it('shows the sprint an item is in even when that sprint is no longer open', async () => {
    const api = {
      getWorkItems: vi.fn().mockResolvedValue([{ id: 1, title: 'Old work', type: 'Story', status: 'completed', priority: 'low', sprintId: 7, sprintName: 'Sprint Zero', labels: [] }]),
      getProjectLabels: vi.fn().mockResolvedValue([]),
      getSprints: vi.fn().mockResolvedValue([sprint({ id: 1 })]),
    };
    wrap(<PmBacklog apiService={api} project={project} members={members} />);
    await screen.findByText('Old work');
    await waitFor(() => expect(screen.getByLabelText('Sprint for Old work')).toHaveTextContent('Sprint Zero'));
  });
});

describe('WorkItemModal effort', () => {
  const item = { id: 10, title: 'Login', description: '', type: 'Story', status: 'todo', priority: 'medium', assignedToId: 1, parentId: null, hasDueDate: false };
  const api = () => ({
    getWorkItemEffort: vi.fn().mockResolvedValue({ itemId: 10, total: 0, people: [{ userId: 1, userName: 'Ada Lovelace', total: 0, cells: [] }] }),
    getWorkItemComments: vi.fn().mockResolvedValue([]), getWorkItemChecklist: vi.fn().mockResolvedValue([]),
    getWorkItemLinks: vi.fn().mockResolvedValue([]), getWorkItemAttachments: vi.fn().mockResolvedValue([]),
    getProjectLabels: vi.fn().mockResolvedValue([]), getWorkItemLookup: vi.fn().mockResolvedValue([]),
  });

  it('warns about unsaved effort, and a quarter point does not block saving the item', async () => {
    const onSave = vi.fn().mockResolvedValue({});
    const user = userEvent.setup();
    wrap(<WorkItemModal item={item} items={[item]} members={members} canEdit apiService={api()} role="ScrumMaster" project={project} onSave={onSave} onClose={vi.fn()} />);

    const box = await screen.findByLabelText('Dev effort');
    expect(screen.queryByText(/unsaved effort changes/i)).not.toBeInTheDocument();
    await user.type(box, '2.25');
    expect(screen.getByText(/unsaved effort changes/i)).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Save' }));
    await waitFor(() => expect(onSave).toHaveBeenCalled());   // not stopped by the browser's number validation
  });
});
