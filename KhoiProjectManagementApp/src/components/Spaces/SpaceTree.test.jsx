// src/components/Spaces/SpaceTree.test.jsx
// SpaceTree flattens the backend's parent/child Space hierarchy into a lazily-expanded tree: the
// root call is GET /spaces (no parentSpaceId), and each node's children are only fetched - via
// GET /spaces?parentSpaceId=<id> - the first time that node is expanded. These tests exercise that
// real fetch()-driven behavior through ApiService + MSW (per CLAUDE.md's testing stack), not by
// stubbing apiService methods, so a broken query string or a broken response-shape assumption would
// actually be caught.
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { server, API_BASE_URL } from '../../test/mswServer';
import ApiService from '../../services/ApiService';
import SpaceTree from './SpaceTree';

const mockSpacesEndpoint = (byParent) => {
  server.use(
    http.get(`${API_BASE_URL}/spaces`, ({ request }) => {
      const url = new URL(request.url);
      const parentSpaceId = url.searchParams.get('parentSpaceId');
      const key = parentSpaceId === null ? 'root' : parentSpaceId;
      if (!(key in byParent)) return HttpResponse.json([]);
      return HttpResponse.json(byParent[key]);
    })
  );
};

describe('SpaceTree', () => {
  let apiService;

  beforeEach(() => {
    localStorage.clear();
    apiService = new ApiService();
  });

  it('shows a loading state before the root spaces arrive', async () => {
    server.use(http.get(`${API_BASE_URL}/spaces`, () => new Promise(() => {}))); // never resolves

    render(<SpaceTree apiService={apiService} selectedSpaceId={null} onSelect={vi.fn()} />);

    expect(screen.getByText(/loading spaces/i)).toBeInTheDocument();
  });

  it('shows the empty state when there are no root spaces', async () => {
    mockSpacesEndpoint({ root: [] });

    render(<SpaceTree apiService={apiService} selectedSpaceId={null} onSelect={vi.fn()} />);

    expect(await screen.findByText(/no spaces available to you yet/i)).toBeInTheDocument();
  });

  it('shows an error message (not a crash) when the root request fails', async () => {
    server.use(
      http.get(`${API_BASE_URL}/spaces`, () => HttpResponse.json({ message: 'Could not load spaces' }, { status: 500 }))
    );

    render(<SpaceTree apiService={apiService} selectedSpaceId={null} onSelect={vi.fn()} />);

    expect(await screen.findByText(/error: could not load spaces/i)).toBeInTheDocument();
  });

  it('renders the root spaces returned by the API', async () => {
    mockSpacesEndpoint({ root: [{ id: 1, name: 'Vault Root' }, { id: 2, name: 'Wiki Root' }] });

    render(<SpaceTree apiService={apiService} selectedSpaceId={null} onSelect={vi.fn()} />);

    expect(await screen.findByText('Vault Root')).toBeInTheDocument();
    expect(screen.getByText('Wiki Root')).toBeInTheDocument();
  });

  it('preselects the first root space on load, like a real click, when nothing is selected yet', async () => {
    const roots = [{ id: 1, name: 'Vault Root' }, { id: 2, name: 'Wiki Root' }];
    mockSpacesEndpoint({ root: roots });
    const onSelect = vi.fn();

    render(<SpaceTree apiService={apiService} selectedSpaceId={null} onSelect={onSelect} />);

    await waitFor(() => expect(onSelect).toHaveBeenCalledWith(roots[0]));
    expect(onSelect).toHaveBeenCalledTimes(1);
  });

  it('does not auto-select anything when a space is already selected', async () => {
    mockSpacesEndpoint({ root: [{ id: 1, name: 'Vault Root' }] });
    const onSelect = vi.fn();

    render(<SpaceTree apiService={apiService} selectedSpaceId={1} onSelect={onSelect} />);

    await screen.findByText('Vault Root');
    expect(onSelect).not.toHaveBeenCalled();
  });

  it('calls onSelect with the clicked space when its name is clicked', async () => {
    const roots = [{ id: 1, name: 'Vault Root' }, { id: 2, name: 'Wiki Root' }];
    mockSpacesEndpoint({ root: roots });
    const onSelect = vi.fn();
    const user = userEvent.setup();

    render(<SpaceTree apiService={apiService} selectedSpaceId={1} onSelect={onSelect} />);
    await screen.findByText('Wiki Root');

    await user.click(screen.getByText('Wiki Root'));

    expect(onSelect).toHaveBeenCalledWith(roots[1]);
  });

  it('highlights the currently selected space and not the others', async () => {
    mockSpacesEndpoint({ root: [{ id: 1, name: 'Vault Root' }, { id: 2, name: 'Wiki Root' }] });

    render(<SpaceTree apiService={apiService} selectedSpaceId={2} onSelect={vi.fn()} />);
    await screen.findByText('Vault Root');

    const selectedRow = screen.getByText('Wiki Root').closest('div');
    const unselectedRow = screen.getByText('Vault Root').closest('div');
    expect(selectedRow).toHaveClass('bg-blue-50');
    expect(unselectedRow).not.toHaveClass('bg-blue-50');
  });

  it('lazily fetches and renders child spaces the first time a node is expanded', async () => {
    mockSpacesEndpoint({
      root: [{ id: 1, name: 'Vault Root' }],
      1: [{ id: 10, name: 'Contracts' }, { id: 11, name: 'Passwords' }],
    });
    const user = userEvent.setup();

    render(<SpaceTree apiService={apiService} selectedSpaceId={null} onSelect={vi.fn()} />);
    await screen.findByText('Vault Root');
    expect(screen.queryByText('Contracts')).not.toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: /toggle/i }));

    expect(await screen.findByText('Contracts')).toBeInTheDocument();
    expect(screen.getByText('Passwords')).toBeInTheDocument();
  });

  it('shows "No sub-spaces" for a leaf node with no children', async () => {
    mockSpacesEndpoint({
      root: [{ id: 1, name: 'Empty Space' }],
      1: [],
    });
    const user = userEvent.setup();

    render(<SpaceTree apiService={apiService} selectedSpaceId={null} onSelect={vi.fn()} />);
    await screen.findByText('Empty Space');

    await user.click(screen.getByRole('button', { name: /toggle/i }));

    expect(await screen.findByText(/no sub-spaces/i)).toBeInTheDocument();
  });

  it('collapses and re-expands without re-fetching children a second time', async () => {
    let requestCount = 0;
    server.use(
      http.get(`${API_BASE_URL}/spaces`, ({ request }) => {
        const url = new URL(request.url);
        const parentSpaceId = url.searchParams.get('parentSpaceId');
        if (parentSpaceId === null) return HttpResponse.json([{ id: 1, name: 'Vault Root' }]);
        requestCount += 1;
        return HttpResponse.json([{ id: 10, name: 'Contracts' }]);
      })
    );
    const user = userEvent.setup();

    render(<SpaceTree apiService={apiService} selectedSpaceId={null} onSelect={vi.fn()} />);
    await screen.findByText('Vault Root');
    const toggle = screen.getByRole('button', { name: /toggle/i });

    await user.click(toggle); // expand -> fetches children
    await screen.findByText('Contracts');
    await user.click(toggle); // collapse
    expect(screen.queryByText('Contracts')).not.toBeInTheDocument();
    await user.click(toggle); // re-expand -> should reuse cached children, not re-fetch
    expect(await screen.findByText('Contracts')).toBeInTheDocument();

    expect(requestCount).toBe(1);
  });

  it('renders nested grandchild spaces at increasing depth', async () => {
    mockSpacesEndpoint({
      root: [{ id: 1, name: 'Vault Root' }],
      1: [{ id: 10, name: 'Contracts' }],
      10: [{ id: 100, name: 'NDA Template' }],
    });
    const user = userEvent.setup();

    render(<SpaceTree apiService={apiService} selectedSpaceId={null} onSelect={vi.fn()} />);
    await screen.findByText('Vault Root');

    await user.click(screen.getAllByRole('button', { name: /toggle/i })[0]);
    await screen.findByText('Contracts');
    await user.click(screen.getAllByRole('button', { name: /toggle/i })[1]);

    expect(await screen.findByText('NDA Template')).toBeInTheDocument();
  });
});
