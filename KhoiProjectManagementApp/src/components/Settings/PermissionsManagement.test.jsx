import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, it, expect, beforeEach } from 'vitest';
import { server, API_BASE_URL } from '../../test/mswServer';
import { ToastProvider } from '../../contexts/ToastContext';
import ApiService from '../../services/ApiService';
import PermissionsManagement from './PermissionsManagement';

// Two roles, two permissions spread across two resources - enough to exercise the resource
// grouping, the "which names are checked" logic, and role switching without being unwieldy.
const roles = [
  { id: 1, name: 'Admin', description: 'Full access', isSystemRole: true },
  { id: 2, name: 'Contributor', description: 'Limited access', isSystemRole: false },
];

const allPermissions = [
  { id: 10, resource: 'projects', action: 'view', name: 'projects.view', description: 'View projects' },
  { id: 11, resource: 'projects', action: 'delete', name: 'projects.delete', description: 'Delete projects' },
  { id: 12, resource: 'vault', action: 'reveal', name: 'vault.reveal', description: 'Reveal secrets' },
];

const renderComponent = () => {
  const apiService = new ApiService();
  return render(
    <ToastProvider>
      <PermissionsManagement apiService={apiService} />
    </ToastProvider>
  );
};

const mockBaseHandlers = ({ rolePermissions = ['projects.view'] } = {}) => {
  server.use(
    http.get(`${API_BASE_URL}/roles`, () => HttpResponse.json(roles)),
    http.get(`${API_BASE_URL}/permissions`, () => HttpResponse.json(allPermissions)),
    http.get(`${API_BASE_URL}/roles/:roleId/permissions`, ({ params }) => {
      // Only Admin (role 1) is asked about in most tests; keep this generic so role-switching
      // tests can still get a sane (empty) response for whichever role they select.
      if (Number(params.roleId) === 1) return HttpResponse.json(rolePermissions);
      return HttpResponse.json([]);
    })
  );
};

describe('PermissionsManagement', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it('shows a loading state before roles and permissions arrive', () => {
    mockBaseHandlers();
    renderComponent();

    expect(screen.getByText(/loading roles and permissions/i)).toBeInTheDocument();
  });

  it('auto-selects the first role and renders its checked permissions grouped by resource', async () => {
    mockBaseHandlers({ rolePermissions: ['projects.view'] });
    renderComponent();

    // "Admin" appears both in the role list and in the detail panel header once loaded, so wait on
    // the (unambiguous) sidebar button rather than the plain text.
    expect(await screen.findByRole('button', { name: /admin/i })).toBeInTheDocument();
    // Resource group headers.
    expect(await screen.findByText('projects')).toBeInTheDocument();
    expect(screen.getByText('vault')).toBeInTheDocument();

    // The permission grid (resource headers/rows) renders as soon as the full permission list
    // loads, before the role's own granted-permissions fetch resolves - the checkboxes exist
    // immediately but start unchecked, so their checked state needs its own wait. Re-query fresh on
    // each retry rather than reusing a reference captured before the state update, in case that
    // update re-renders new checkbox nodes rather than mutating these ones.
    await waitFor(() => expect(screen.getByRole('checkbox', { name: /view projects/i })).toBeChecked());
    expect(screen.getByRole('checkbox', { name: /delete projects/i })).not.toBeChecked();
    expect(screen.getByRole('checkbox', { name: /reveal secrets/i })).not.toBeChecked();
  });

  it('toggling a permission and saving PUTs the role id and the full updated permission-name set', async () => {
    mockBaseHandlers({ rolePermissions: ['projects.view'] });
    let putRoleId = null;
    let putBody = null;
    server.use(
      http.put(`${API_BASE_URL}/roles/:roleId/permissions`, async ({ params, request }) => {
        putRoleId = params.roleId;
        putBody = await request.json();
        return HttpResponse.json({});
      })
    );

    const user = userEvent.setup();
    renderComponent();

    await screen.findByRole('button', { name: /admin/i });
    const deleteCheckbox = await screen.findByRole('checkbox', { name: /delete projects/i });
    const saveButton = screen.getByRole('button', { name: /save changes/i });
    expect(saveButton).toBeDisabled();

    await user.click(deleteCheckbox);
    expect(saveButton).toBeEnabled();

    await user.click(saveButton);

    await waitFor(() => expect(putBody).not.toBeNull());
    expect(putRoleId).toBe('1');
    expect(putBody.permissionNames).toEqual(expect.arrayContaining(['projects.view', 'projects.delete']));
    expect(putBody.permissionNames).toHaveLength(2);
    expect(await screen.findByText(/permissions updated for admin/i)).toBeInTheDocument();
  });

  it('toasts the server-provided error message when the initial load fails', async () => {
    server.use(
      http.get(`${API_BASE_URL}/roles`, () => HttpResponse.json({ message: 'Roles are unavailable right now' }, { status: 500 })),
      http.get(`${API_BASE_URL}/permissions`, () => HttpResponse.json([]))
    );
    renderComponent();

    expect(await screen.findByText('Roles are unavailable right now')).toBeInTheDocument();
    // Never leaves the loading state since roles/allPermissions never get set.
    expect(screen.getByText(/loading roles and permissions/i)).toBeInTheDocument();
  });

  it('toasts an error and keeps the change pending when saving permissions fails', async () => {
    mockBaseHandlers({ rolePermissions: ['projects.view'] });
    server.use(
      http.put(`${API_BASE_URL}/roles/:roleId/permissions`, () =>
        HttpResponse.json({ message: 'Save failed' }, { status: 500 }))
    );

    const user = userEvent.setup();
    renderComponent();

    await screen.findByRole('button', { name: /admin/i });
    const deleteCheckbox = await screen.findByRole('checkbox', { name: /delete projects/i });
    await user.click(deleteCheckbox);
    await user.click(screen.getByRole('button', { name: /save changes/i }));

    expect(await screen.findByText('Save failed')).toBeInTheDocument();
    // Still dirty (save never succeeded), so the button stays enabled for a retry.
    expect(screen.getByRole('button', { name: /save changes/i })).toBeEnabled();
  });

  it('creates a new role with the entered name/description and selects it', async () => {
    mockBaseHandlers({ rolePermissions: [] });
    let postBody = null;
    server.use(
      http.post(`${API_BASE_URL}/roles`, async ({ request }) => {
        postBody = await request.json();
        return HttpResponse.json({ id: 3, name: postBody.name, description: postBody.description, isSystemRole: false }, { status: 201 });
      }),
      http.get(`${API_BASE_URL}/roles/3/permissions`, () => HttpResponse.json([]))
    );

    const user = userEvent.setup();
    renderComponent();

    await screen.findByRole('button', { name: /admin/i });
    await user.click(screen.getByRole('button', { name: /new role/i }));

    const modal = screen.getByRole('heading', { name: 'New Role' }).closest('div.bg-white');
    await user.type(within(modal).getByPlaceholderText(/role name/i), 'Reviewer');
    await user.type(within(modal).getByPlaceholderText(/description/i), 'Reviews work');
    await user.click(within(modal).getByRole('button', { name: /^create$/i }));

    await waitFor(() => expect(postBody).not.toBeNull());
    expect(postBody).toEqual({ name: 'Reviewer', description: 'Reviews work' });
    expect(await screen.findByText(/role created/i)).toBeInTheDocument();
    // The modal (its heading) is gone; the page's own "New Role" button remains.
    expect(screen.queryByRole('heading', { name: 'New Role' })).not.toBeInTheDocument();
  });
});
