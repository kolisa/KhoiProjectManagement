import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, it, expect, beforeEach } from 'vitest';
import { server, API_BASE_URL } from '../../test/mswServer';
import { ToastProvider } from '../../contexts/ToastContext';
import ApiService from '../../services/ApiService';
import GroupsManagement from './GroupsManagement';

const teamMembers = [
  { id: 1, name: 'Ada Lovelace', position: 'Engineer' },
  { id: 2, name: 'Grace Hopper', position: 'Architect' },
];

const groups = [
  { id: 100, name: 'Engineering Leads', description: 'Leads group', memberCount: 1 },
  { id: 200, name: 'On-call', description: '', memberCount: 0 },
];

const renderComponent = (props = {}) => {
  const apiService = new ApiService();
  return render(
    <ToastProvider>
      <GroupsManagement apiService={apiService} teamMembers={teamMembers} {...props} />
    </ToastProvider>
  );
};

const mockBaseHandlers = ({ groupsList = groups, memberIds = [1] } = {}) => {
  server.use(
    http.get(`${API_BASE_URL}/groups`, () => HttpResponse.json(groupsList)),
    http.get(`${API_BASE_URL}/groups/:groupId/members`, ({ params }) => {
      if (Number(params.groupId) === groupsList[0]?.id) return HttpResponse.json(memberIds);
      return HttpResponse.json([]);
    })
  );
};

describe('GroupsManagement', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it('shows a loading state before groups arrive', () => {
    mockBaseHandlers();
    renderComponent();

    expect(screen.getByText(/loading groups/i)).toBeInTheDocument();
  });

  it('shows the empty state when there are no groups yet', async () => {
    mockBaseHandlers({ groupsList: [] });
    renderComponent();

    expect(await screen.findByText(/no groups yet/i)).toBeInTheDocument();
    expect(screen.getByText(/select a group to edit its members/i)).toBeInTheDocument();
  });

  it('auto-selects the first group and checks its current members', async () => {
    mockBaseHandlers({ memberIds: [1] });
    renderComponent();

    // "Engineering Leads" can render in both the group list and the (once-loaded) detail panel
    // header, so wait on the unambiguous sidebar button rather than the plain text.
    expect(await screen.findByRole('button', { name: /engineering leads/i })).toBeInTheDocument();
    expect(screen.getByText('On-call')).toBeInTheDocument();

    await screen.findByRole('checkbox', { name: /ada lovelace/i });
    // findByRole only waits for the checkbox to exist, not for the separate member-ids fetch that
    // sets its checked state - that needs its own wait, re-querying fresh each retry rather than a
    // reference captured before the state update (which may point at a now-replaced DOM node).
    await waitFor(() => expect(screen.getByRole('checkbox', { name: /ada lovelace/i })).toBeChecked());
    expect(screen.getByRole('checkbox', { name: /grace hopper/i })).not.toBeChecked();
  });

  it('adding and removing a member and saving PUTs the group id and the full updated member-id set', async () => {
    mockBaseHandlers({ memberIds: [1] });
    let putGroupId = null;
    let putBody = null;
    server.use(
      http.put(`${API_BASE_URL}/groups/:groupId/members`, async ({ params, request }) => {
        putGroupId = params.groupId;
        putBody = await request.json();
        return HttpResponse.json({});
      })
    );

    const user = userEvent.setup();
    renderComponent();

    await screen.findByRole('button', { name: /engineering leads/i });
    const adaCheckbox = await screen.findByRole('checkbox', { name: /ada lovelace/i });
    const graceCheckbox = screen.getByRole('checkbox', { name: /grace hopper/i });
    const saveButton = screen.getByRole('button', { name: /save changes/i });
    expect(saveButton).toBeDisabled();

    // Remove Ada, add Grace.
    await user.click(adaCheckbox);
    await user.click(graceCheckbox);
    expect(saveButton).toBeEnabled();

    await user.click(saveButton);

    await waitFor(() => expect(putBody).not.toBeNull());
    expect(putGroupId).toBe('100');
    expect(putBody.userIds).toEqual([2]);
    expect(await screen.findByText(/members updated for engineering leads/i)).toBeInTheDocument();
  });

  it('toasts the server-provided error message when the initial load fails', async () => {
    server.use(
      http.get(`${API_BASE_URL}/groups`, () => HttpResponse.json({ message: 'Groups are unavailable' }, { status: 500 }))
    );
    renderComponent();

    expect(await screen.findByText('Groups are unavailable')).toBeInTheDocument();
    expect(screen.getByText(/loading groups/i)).toBeInTheDocument();
  });

  it('creates a new group with the entered name/description and selects it', async () => {
    mockBaseHandlers({ memberIds: [] });
    let postBody = null;
    server.use(
      http.post(`${API_BASE_URL}/groups`, async ({ request }) => {
        postBody = await request.json();
        return HttpResponse.json({ id: 300, name: postBody.name, description: postBody.description, memberCount: 0 }, { status: 201 });
      }),
      http.get(`${API_BASE_URL}/groups/300/members`, () => HttpResponse.json([]))
    );

    const user = userEvent.setup();
    renderComponent();

    await screen.findByRole('button', { name: /engineering leads/i });
    await user.click(screen.getByRole('button', { name: /new group/i }));

    const modal = screen.getByRole('heading', { name: 'New Group' }).closest('div.bg-white');
    await user.type(within(modal).getByPlaceholderText(/group name/i), 'Support');
    await user.type(within(modal).getByPlaceholderText(/description/i), 'Support rota');
    await user.click(within(modal).getByRole('button', { name: /^create$/i }));

    await waitFor(() => expect(postBody).not.toBeNull());
    expect(postBody).toEqual({ name: 'Support', description: 'Support rota' });
    expect(await screen.findByText(/group created/i)).toBeInTheDocument();
    // The modal (its heading) is gone; the page's own "New Group" button remains.
    expect(screen.queryByRole('heading', { name: 'New Group' })).not.toBeInTheDocument();
  });
});
