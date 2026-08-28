// src/components/Spaces/ManageAccessModal.test.jsx
// Covers the actual permission-editing UI for a Space (Vault category / Wiki space / Library
// folder) - loading existing grants, gating Role/Group grantees behind users.manage_roles /
// groups.manage the same way the component does, adding/removing/changing grants locally, and
// saving them via PUT /spaces/:id/permissions with the exact payload shape the backend expects
// (userId/roleId/groupId/level - see SetSpacePermissions on SpacesController). Misrepresenting who
// has access to a Space is the highest-stakes bug this component could ship, so the save-payload
// assertions are deliberately exact rather than "was called".
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { server, API_BASE_URL } from '../../test/mswServer';
import { ToastProvider } from '../../contexts/ToastContext';
import ApiService from '../../services/ApiService';
import ManageAccessModal from './ManageAccessModal';

const space = { id: 5, name: 'Contracts' };
const teamMembers = [
  { id: 1, name: 'Alice' },
  { id: 2, name: 'Bob' },
];

const adminUser = { permissions: ['users.manage_roles', 'groups.manage'] };
const plainUser = { permissions: [] };

const renderModal = (props = {}) => {
  const apiService = new ApiService();
  const onClose = vi.fn();
  render(
    <ToastProvider>
      <ManageAccessModal
        apiService={apiService}
        space={space}
        teamMembers={teamMembers}
        currentUser={adminUser}
        onClose={onClose}
        {...props}
      />
    </ToastProvider>
  );
  return { onClose };
};

const mockPermissions = (grants) => {
  server.use(http.get(`${API_BASE_URL}/spaces/${space.id}/permissions`, () => HttpResponse.json(grants)));
};

describe('ManageAccessModal', () => {
  beforeEach(() => {
    localStorage.clear();
    // Roles/Groups aren't relevant to most tests but the component fetches them whenever the
    // current user is allowed to see them, so give every test a harmless default response instead
    // of repeating this handler everywhere - onUnhandledRequest: 'error' would otherwise fail any
    // test that grants adminUser but forgets it.
    server.use(
      http.get(`${API_BASE_URL}/roles`, () => HttpResponse.json([{ id: 20, name: 'Managers' }])),
      http.get(`${API_BASE_URL}/groups`, () => HttpResponse.json([{ id: 30, name: 'Finance Team' }]))
    );
  });

  it('shows a loading state before existing access has loaded', () => {
    server.use(http.get(`${API_BASE_URL}/spaces/${space.id}/permissions`, () => new Promise(() => {})));

    renderModal();

    expect(screen.getByText(/loading/i)).toBeInTheDocument();
  });

  it('shows the empty state when nobody has explicit access yet', async () => {
    mockPermissions([]);

    renderModal();

    expect(await screen.findByText(/nobody has explicit access yet/i)).toBeInTheDocument();
  });

  it('renders each existing grant with its name and level', async () => {
    mockPermissions([
      { id: 100, userId: 1, roleId: null, groupId: null, userName: 'Alice', level: 'Write' },
    ]);

    renderModal();

    expect(await screen.findByText('Alice')).toBeInTheDocument();
    const row = screen.getByText('Alice').closest('div');
    expect(within(row).getByRole('combobox')).toHaveValue('Write');
  });

  it('labels role and group grants distinctly from person grants', async () => {
    mockPermissions([
      { id: 100, userId: 1, roleId: null, groupId: null, userName: 'Alice', level: 'Read' },
      { id: 101, userId: null, roleId: 20, groupId: null, roleName: 'Managers', level: 'Manage' },
      { id: 102, userId: null, roleId: null, groupId: 30, groupName: 'Finance Team', level: 'Read' },
    ]);

    renderModal();

    // Rows are located via their (uniquely-named) remove buttons rather than getByText on the name,
    // since a role/group row's name and its "(role)"/"(group)" suffix live in the same <span> - an
    // exact-text getByText('Managers') would fail to match "Managers(role)".
    await screen.findByRole('button', { name: /remove alice/i });
    const aliceRow = screen.getByRole('button', { name: /remove alice/i }).closest('div');
    const managersRow = screen.getByRole('button', { name: /remove managers/i }).closest('div');
    const financeRow = screen.getByRole('button', { name: /remove finance team/i }).closest('div');

    expect(aliceRow).toHaveTextContent('Alice');
    expect(aliceRow).not.toHaveTextContent('(role)');
    expect(aliceRow).not.toHaveTextContent('(group)');
    expect(managersRow).toHaveTextContent('Managers');
    expect(managersRow).toHaveTextContent('(role)');
    expect(financeRow).toHaveTextContent('Finance Team');
    expect(financeRow).toHaveTextContent('(group)');
  });

  it('reports the error and closes the modal when loading access fails', async () => {
    server.use(
      http.get(`${API_BASE_URL}/spaces/${space.id}/permissions`, () =>
        HttpResponse.json({ message: 'Could not load access for this space.' }, { status: 500 }))
    );

    const { onClose } = renderModal();

    await waitFor(() => expect(onClose).toHaveBeenCalled());
    expect(await screen.findByRole('alert')).toHaveTextContent(/could not load access for this space/i);
  });

  it('only offers the Person grantee type when the current user cannot manage roles or groups', async () => {
    mockPermissions([]);

    renderModal({ currentUser: plainUser });

    await screen.findByText(/nobody has explicit access yet/i);
    // No grantee-type selector at all - straight to "Add a person...".
    expect(screen.getByDisplayValue(/add a person/i)).toBeInTheDocument();
    expect(screen.queryByText('Role')).not.toBeInTheDocument();
    expect(screen.queryByText('Group')).not.toBeInTheDocument();
  });

  it('offers Role and Group grantee types when the current user is allowed to see them', async () => {
    mockPermissions([]);

    renderModal({ currentUser: adminUser });

    await screen.findByText(/nobody has explicit access yet/i);
    const typeSelect = screen.getByDisplayValue('Person');
    expect(within(typeSelect).getByText('Role')).toBeInTheDocument();
    expect(within(typeSelect).getByText('Group')).toBeInTheDocument();
  });

  it('only lists team members who do not already have a grant in the "add a person" dropdown', async () => {
    mockPermissions([{ id: 100, userId: 1, roleId: null, groupId: null, userName: 'Alice', level: 'Read' }]);

    renderModal();

    await screen.findByText('Alice');
    const addSelect = screen.getByDisplayValue(/add a person/i);
    expect(within(addSelect).queryByText('Alice')).not.toBeInTheDocument();
    expect(within(addSelect).getByText('Bob')).toBeInTheDocument();
  });

  it('adds a new person grant locally at the default Read level', async () => {
    mockPermissions([]);
    const user = userEvent.setup();

    renderModal();
    await screen.findByText(/nobody has explicit access yet/i);

    await user.selectOptions(screen.getByDisplayValue(/add a person/i), '2'); // Bob
    await user.click(screen.getByRole('button', { name: 'Add' }));

    const row = await screen.findByText('Bob');
    expect(within(row.closest('div')).getByRole('combobox')).toHaveValue('Read');
    expect(screen.queryByText(/nobody has explicit access yet/i)).not.toBeInTheDocument();
  });

  it('does not add anything when no grantee is selected', async () => {
    mockPermissions([]);
    renderModal();
    await screen.findByText(/nobody has explicit access yet/i);

    expect(screen.getByRole('button', { name: 'Add' })).toBeDisabled();
  });

  it('changes an existing grant\'s level via its dropdown', async () => {
    mockPermissions([{ id: 100, userId: 1, roleId: null, groupId: null, userName: 'Alice', level: 'Read' }]);
    const user = userEvent.setup();

    renderModal();
    const row = (await screen.findByText('Alice')).closest('div');

    await user.selectOptions(within(row).getByRole('combobox'), 'Manage');

    expect(within(row).getByRole('combobox')).toHaveValue('Manage');
  });

  it('removes a grant when its trash button is clicked', async () => {
    mockPermissions([{ id: 100, userId: 1, roleId: null, groupId: null, userName: 'Alice', level: 'Read' }]);
    const user = userEvent.setup();

    renderModal();
    await screen.findByText('Alice');

    await user.click(screen.getByRole('button', { name: /remove alice/i }));

    // Scoped away from <option> - removing the grant makes Alice selectable again in the "add a
    // person" dropdown, which also renders her name as option text.
    expect(screen.queryByText('Alice', { selector: ':not(option)' })).not.toBeInTheDocument();
    expect(await screen.findByText(/nobody has explicit access yet/i)).toBeInTheDocument();
  });

  it('saves the exact grant payload (userId/roleId/groupId/level) and closes with a success toast', async () => {
    mockPermissions([{ id: 100, userId: 1, roleId: null, groupId: null, userName: 'Alice', level: 'Read' }]);
    let putBody = null;
    server.use(
      http.put(`${API_BASE_URL}/spaces/${space.id}/permissions`, async ({ request }) => {
        putBody = await request.json();
        return HttpResponse.json({ success: true });
      })
    );
    const user = userEvent.setup();

    const { onClose } = renderModal();
    const row = (await screen.findByText('Alice')).closest('div');
    await user.selectOptions(within(row).getByRole('combobox'), 'Write');
    await user.click(screen.getByRole('button', { name: /^save$/i }));

    await waitFor(() => expect(putBody).not.toBeNull());
    expect(putBody).toEqual([{ userId: 1, roleId: null, groupId: null, level: 'Write' }]);
    expect(await screen.findByRole('status')).toHaveTextContent(/access updated/i);
    expect(onClose).toHaveBeenCalled();
  });

  it('shows an error toast and keeps the modal open when saving fails', async () => {
    mockPermissions([{ id: 100, userId: 1, roleId: null, groupId: null, userName: 'Alice', level: 'Read' }]);
    server.use(
      http.put(`${API_BASE_URL}/spaces/${space.id}/permissions`, () =>
        HttpResponse.json({ message: 'Could not save access changes.' }, { status: 500 }))
    );
    const user = userEvent.setup();

    const { onClose } = renderModal();
    await screen.findByText('Alice');
    await user.click(screen.getByRole('button', { name: /^save$/i }));

    expect(await screen.findByRole('alert')).toHaveTextContent(/could not save access changes/i);
    expect(onClose).not.toHaveBeenCalled();
  });

  it('calls onClose when Cancel is clicked without saving', async () => {
    mockPermissions([]);
    const user = userEvent.setup();

    const { onClose } = renderModal();
    await screen.findByText(/nobody has explicit access yet/i);
    await user.click(screen.getByRole('button', { name: /cancel/i }));

    expect(onClose).toHaveBeenCalled();
  });
});
