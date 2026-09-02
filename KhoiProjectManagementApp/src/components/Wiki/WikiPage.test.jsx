import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { server, API_BASE_URL } from '../../test/mswServer';
import { ToastProvider } from '../../contexts/ToastContext';
import { ConfirmProvider } from '../../contexts/ConfirmContext';
import ApiService from '../../services/ApiService';
import WikiPage from './WikiPage';

// WikiPage delegates space-listing to SpaceTree (../Spaces/SpaceTree.jsx), which calls
// apiService.getSpaces(null) -> GET /spaces (no query string, per ApiService.getSpaces) on mount,
// and auto-selects the first root space the moment it loads. That auto-select is what drives
// WikiPage's own `GET /wiki/pages?spaceId=...` effect - there's no separate "pick a space" step
// needed in these tests once a spaces list is mocked.
const testUser = {
  id: 1,
  name: 'Test Admin',
  email: 'admin@khoitech.africa',
  permissions: ['spaces.manage'],
};

const renderWikiPage = (props = {}) => {
  const apiService = new ApiService();
  return render(
    <ToastProvider>
      <ConfirmProvider>
        <WikiPage apiService={apiService} user={testUser} teamMembers={[]} {...props} />
      </ConfirmProvider>
    </ToastProvider>
  );
};

const mockSpaces = (spaces) => {
  server.use(http.get(`${API_BASE_URL}/spaces`, () => HttpResponse.json(spaces)));
};

const mockPages = (pages) => {
  server.use(http.get(`${API_BASE_URL}/wiki/pages`, () => HttpResponse.json(pages)));
};

describe('WikiPage', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  afterEach(() => {
    localStorage.clear();
  });

  it('shows the empty state when there are no wiki spaces yet', async () => {
    mockSpaces([]);

    renderWikiPage();

    expect(await screen.findByText(/no spaces available to you yet/i)).toBeInTheDocument();
    expect(screen.getByText(/select a wiki space on the left to browse its pages/i)).toBeInTheDocument();
  });

  it('renders the auto-selected space\'s pages once loaded', async () => {
    mockSpaces([{ id: 1, name: 'Engineering', myEffectiveLevel: 'Write' }]);
    mockPages([
      { id: 10, title: 'Getting Started', wordCount: 400, labels: [] },
      { id: 11, title: 'Deployment Runbook', wordCount: 0, labels: ['ops'] },
    ]);

    renderWikiPage();

    // SpaceTree renders the space name, then auto-selects it, which flips WikiPage's right pane
    // from the "select a space" placeholder to the breadcrumb + page list. The breadcrumb's space
    // name is a <button> (unlike SpaceTree's plain <span>), so querying by role sidesteps the
    // "Engineering" text appearing twice on screen (once in the tree, once in the breadcrumb).
    expect(await screen.findByRole('button', { name: 'Engineering' })).toBeInTheDocument();
    expect(await screen.findByText('Getting Started')).toBeInTheDocument();
    expect(screen.getByText('Deployment Runbook')).toBeInTheDocument();
    expect(screen.getByText('2 pages')).toBeInTheDocument();
    expect(screen.queryByText(/no pages here yet/i)).not.toBeInTheDocument();
  });

  it('shows the empty page-list state for a space with no pages', async () => {
    mockSpaces([{ id: 1, name: 'Engineering', myEffectiveLevel: 'Write' }]);
    mockPages([]);

    renderWikiPage();

    expect(await screen.findByText(/no pages here yet/i)).toBeInTheDocument();
    expect(screen.getByText('0 pages')).toBeInTheDocument();
  });

  it('shows an error message when loading pages fails, without taking down the rest of the page', async () => {
    mockSpaces([{ id: 1, name: 'Engineering', myEffectiveLevel: 'Write' }]);
    server.use(http.get(`${API_BASE_URL}/wiki/pages`, () =>
      HttpResponse.json({ message: 'Could not load pages' }, { status: 500 })));

    renderWikiPage();

    expect(await screen.findByText('Could not load pages')).toBeInTheDocument();
    // The rest of the shell (heading, search box, space tree) must still be intact.
    expect(screen.getByRole('heading', { name: /^wiki$/i })).toBeInTheDocument();
    expect(screen.getByPlaceholderText(/search wiki pages/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Engineering' })).toBeInTheDocument();
  });

  it('creates a new root wiki space, posts the right body, and refreshes the tree', async () => {
    // No spaces initially - only spaces.manage-holding users see the "New wiki space" affordance,
    // and this test's testUser carries that permission (see hasPermission/canCreateRootSpace).
    mockSpaces([]);

    let postBody = null;
    server.use(
      http.post(`${API_BASE_URL}/spaces`, async ({ request }) => {
        postBody = await request.json();
        return HttpResponse.json({ id: 5, name: postBody.name, myEffectiveLevel: 'Manage' }, { status: 201 });
      })
    );

    const user = userEvent.setup();
    renderWikiPage();

    await screen.findByText(/no spaces available to you yet/i);

    await user.click(screen.getByRole('button', { name: /new wiki space/i }));
    expect(screen.getByRole('heading', { name: /new wiki space/i })).toBeInTheDocument();

    // After the space is created, WikiPage bumps `treeKey` to remount SpaceTree, which re-fetches
    // GET /spaces - serve the created space on that refetch so the tree doesn't error out.
    mockSpaces([{ id: 5, name: 'Product', myEffectiveLevel: 'Manage' }]);
    mockPages([]);

    await user.type(screen.getByPlaceholderText(/space name/i), 'Product');
    await user.click(screen.getByRole('button', { name: /^create$/i }));

    await waitFor(() => expect(postBody).not.toBeNull());
    expect(postBody).toEqual({
      name: 'Product',
      description: '',
      parentSpaceId: null,
      spaceType: 'Generic',
      inheritPermissions: true,
    });

    // Modal closes and a success toast confirms the create.
    expect(screen.queryByRole('heading', { name: /^new wiki space$/i })).not.toBeInTheDocument();
    expect(await screen.findByText('Wiki space created.')).toBeInTheDocument();
    // The refreshed tree now shows the newly created space. SpaceTree auto-selects the first root
    // it loads, so "Product" may render both in the tree and in the breadcrumb by the time this
    // resolves - assert presence rather than a single unique match.
    await waitFor(() => expect(screen.getAllByText('Product').length).toBeGreaterThan(0));
  });

  it('shows the server-provided error inline and keeps the modal open when space creation fails', async () => {
    mockSpaces([]);
    server.use(
      http.post(`${API_BASE_URL}/spaces`, () =>
        HttpResponse.json({ message: 'A space with that name already exists.' }, { status: 400 }))
    );

    const user = userEvent.setup();
    renderWikiPage();

    await screen.findByText(/no spaces available to you yet/i);
    await user.click(screen.getByRole('button', { name: /new wiki space/i }));
    await user.type(screen.getByPlaceholderText(/space name/i), 'Duplicate');
    await user.click(screen.getByRole('button', { name: /^create$/i }));

    expect(await screen.findByText('A space with that name already exists.')).toBeInTheDocument();
    // Modal stays open so the user can fix the name and retry.
    expect(screen.getByRole('heading', { name: /^new wiki space$/i })).toBeInTheDocument();
  });
});
