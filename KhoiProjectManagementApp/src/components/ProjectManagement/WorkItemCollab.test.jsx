import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';
import { ToastProvider } from '../../contexts/ToastContext';
import { ConfirmProvider } from '../../contexts/ConfirmContext';
import WorkItemCollab from './WorkItemCollab';

const members = [{ userId: 1, name: 'Dana Dev' }, { userId: 2, name: 'Sam Smith' }];
const item = { id: 7, title: 'Login page' };

const makeApi = (overrides = {}) => ({
  getWorkItemComments: vi.fn().mockResolvedValue([
    { id: 1, authorName: 'Sam Smith', body: 'Please look @Dana Dev', createdAt: '2026-11-10T08:00:00Z', canEdit: false, canDelete: false },
  ]),
  addWorkItemComment: vi.fn().mockResolvedValue({}),
  getWorkItemChecklist: vi.fn().mockResolvedValue([
    { id: 1, text: 'Write tests', isDone: true, position: 1 },
    { id: 2, text: 'Update docs', isDone: false, position: 2 },
  ]),
  addChecklistItem: vi.fn().mockResolvedValue({}),
  updateChecklistItem: vi.fn().mockResolvedValue({}),
  deleteChecklistItem: vi.fn().mockResolvedValue(null),
  getWorkItemLinks: vi.fn().mockResolvedValue([
    { id: 1, relation: 'Is blocked by', type: 'Blocks', isOutgoing: false, otherId: 3, otherTitle: 'API', otherType: 'Task', otherStatus: 'in-progress' },
  ]),
  addWorkItemLink: vi.fn().mockResolvedValue({}),
  deleteWorkItemLink: vi.fn().mockResolvedValue(null),
  getWorkItemAttachments: vi.fn().mockResolvedValue([]),
  uploadWorkItemAttachment: vi.fn().mockResolvedValue({}),
  ...overrides,
});

const renderCollab = (api, abilities = { collaborate: true, edit: true }) =>
  render(
    <ToastProvider>
      <ConfirmProvider>
        <WorkItemCollab apiService={api} item={item} items={[item, { id: 3, title: 'API' }, { id: 4, title: 'DB' }]} members={members} can={abilities} />
      </ConfirmProvider>
    </ToastProvider>
  );

describe('WorkItemCollab', () => {
  it('shows tab counts and highlights @mentions of project members', async () => {
    renderCollab(makeApi());

    // The name also appears as a mention chip button, so pick the highlighted span inside the comment.
    const highlighted = await waitFor(() => {
      const el = screen.getAllByText('@Dana Dev').find((e) => e.tagName === 'SPAN');
      expect(el).toBeDefined();
      return el;
    });
    expect(highlighted).toHaveClass('text-blue-700');
    expect(screen.getByRole('tab', { name: /comments \(1\)/i })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /checklist \(1\/2\)/i })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /links \(1\)/i })).toBeInTheDocument();
  });

  it('posts a trimmed comment and inserts a mention from the chips', async () => {
    const api = makeApi();
    const user = userEvent.setup();
    renderCollab(api);
    await screen.findByText(/please look/i);

    await user.click(screen.getByRole('button', { name: '@Sam Smith' }));
    expect(screen.getByLabelText(/add a comment/i)).toHaveValue('@Sam Smith ');
    await user.type(screen.getByLabelText(/add a comment/i), 'thanks  ');
    await user.click(screen.getByRole('button', { name: 'Comment' }));

    await waitFor(() => expect(api.addWorkItemComment).toHaveBeenCalledWith(7, '@Sam Smith thanks'));
  });

  it('hides the comment box for a role that cannot collaborate', async () => {
    renderCollab(makeApi(), { collaborate: false, edit: false });
    await screen.findByText(/please look/i);

    expect(screen.queryByLabelText(/add a comment/i)).not.toBeInTheDocument();
    expect(screen.getByText(/can read this discussion but not post/i)).toBeInTheDocument();
  });

  it('toggles a checklist item and reports the new state', async () => {
    const api = makeApi();
    const user = userEvent.setup();
    renderCollab(api);
    await user.click(screen.getByRole('tab', { name: /checklist/i }));

    await user.click(await screen.findByRole('checkbox', { name: 'Update docs' }));

    await waitFor(() => expect(api.updateChecklistItem).toHaveBeenCalledWith(2, 'Update docs', true));
  });

  it('warns when an item is blocked by unfinished work', async () => {
    const user = userEvent.setup();
    renderCollab(makeApi());
    await user.click(screen.getByRole('tab', { name: /links/i }));

    expect(await screen.findByText(/blocked by 1 unfinished item/i)).toBeInTheDocument();
  });

  it('offers only unlinked items when adding a link, and sends the chosen type', async () => {
    const api = makeApi();
    const user = userEvent.setup();
    renderCollab(api);
    await user.click(screen.getByRole('tab', { name: /links/i }));
    await screen.findByText(/blocked by 1/i);

    const target = screen.getByLabelText('Item to link');
    expect(Array.from(target.options).map((o) => o.textContent)).toEqual(['Select an item…', '#4 DB']);

    await user.selectOptions(screen.getByLabelText('Link type'), 'Blocks');
    await user.selectOptions(target, '4');
    await user.click(screen.getByRole('button', { name: 'Link' }));

    await waitFor(() => expect(api.addWorkItemLink).toHaveBeenCalledWith(7, 4, 'Blocks'));
  });

  it('hides editing controls on checklist and links for read-only roles', async () => {
    const user = userEvent.setup();
    renderCollab(makeApi(), { collaborate: false, edit: false });
    await user.click(screen.getByRole('tab', { name: /checklist/i }));
    expect(await screen.findByRole('checkbox', { name: 'Update docs' })).toBeDisabled();
    expect(screen.queryByPlaceholderText(/add an item/i)).not.toBeInTheDocument();

    await user.click(screen.getByRole('tab', { name: /links/i }));
    await screen.findByText(/blocked by 1/i);
    expect(screen.queryByLabelText('Item to link')).not.toBeInTheDocument();
  });
});
