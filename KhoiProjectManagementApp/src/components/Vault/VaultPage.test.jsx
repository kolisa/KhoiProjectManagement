import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { server, API_BASE_URL } from '../../test/mswServer';
import { ToastProvider } from '../../contexts/ToastContext';
import { ConfirmProvider } from '../../contexts/ConfirmContext';
import ApiService from '../../services/ApiService';
import VaultPage from './VaultPage';

// VaultPage delegates category navigation to <SpaceTree>, which calls GET /spaces itself (see
// SpaceTree.jsx) - `getSpaces(null)` for the root list resolves to a bare `/spaces` with no query
// string (ApiService.getSpaces only appends `?parentSpaceId=` when the arg isn't null/undefined), so
// the root handler here must NOT expect that query param. SpaceTree also auto-selects the first root
// category the instant the list loads (its own "never rest on an empty placeholder" behavior), which
// is what drives VaultPage into loading entries without any click - exactly like a real page load.
const rootCategory = { id: 10, name: 'Infra Secrets', myEffectiveLevel: 'Manage' };

const spacesRootHandler = (roots = [rootCategory]) =>
  http.get(`${API_BASE_URL}/spaces`, ({ request }) => {
    const url = new URL(request.url);
    // A real "list children of category 10" call also lands on plain /spaces but with a
    // parentSpaceId query param - only the parent-less root call should get the root list.
    if (url.searchParams.has('parentSpaceId')) return HttpResponse.json([]);
    return HttpResponse.json(roots);
  });

const granteeCountHandler = (count = 0) =>
  http.get(`${API_BASE_URL}/spaces/:id/grantee-count`, () => HttpResponse.json(count));

const testUser = { id: 1, name: 'Test Admin', email: 'admin@khoitech.africa', permissions: ['spaces.manage'] };

const renderVaultPage = (props = {}) =>
  render(
    <ToastProvider>
      <ConfirmProvider>
        <VaultPage apiService={new ApiService()} user={testUser} teamMembers={[]} {...props} />
      </ConfirmProvider>
    </ToastProvider>
  );

describe('VaultPage', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it('loads the first category automatically and renders its entries from the API response', async () => {
    server.use(
      spacesRootHandler(),
      granteeCountHandler(2),
      http.get(`${API_BASE_URL}/vault/entries`, ({ request }) => {
        const url = new URL(request.url);
        expect(url.searchParams.get('spaceId')).toBe('10');
        return HttpResponse.json([
          { id: 100, name: 'Prod DB', systemOrUrl: 'db.prod.internal', username: 'admin' },
          { id: 101, name: 'CI Token', systemOrUrl: 'github.com', username: '' },
        ]);
      })
    );

    renderVaultPage();

    expect(await screen.findByRole('heading', { name: 'Infra Secrets' })).toBeInTheDocument();
    expect(await screen.findByText('Prod DB')).toBeInTheDocument();
    expect(screen.getByText('CI Token')).toBeInTheDocument();
    // VaultPage renders the count as adjacent JSX expressions ({entries.length} entr{'y'/'ies'})
    // followed by the grantee-count text in the same <p>, so no single node's full textContent is
    // exactly "2 entries" - match on a prefix of the containing element's combined textContent
    // instead of a single-node exact-text query.
    expect(screen.getByText((_, el) => el.textContent.startsWith('2 entries'))).toBeInTheDocument();
    // Sharing count surfaces from the separate grantee-count call.
    expect(screen.getByText(/shared with 2 people/i)).toBeInTheDocument();
  });

  it('shows the empty state when a category has no entries', async () => {
    server.use(
      spacesRootHandler(),
      granteeCountHandler(0),
      http.get(`${API_BASE_URL}/vault/entries`, () => HttpResponse.json([]))
    );

    renderVaultPage();

    expect(await screen.findByText(/no entries in this category yet/i)).toBeInTheDocument();
    expect(screen.getByText('0 entries')).toBeInTheDocument();
  });

  it('shows an error message without crashing the rest of the page when loading entries fails', async () => {
    server.use(
      spacesRootHandler(),
      granteeCountHandler(0),
      http.get(`${API_BASE_URL}/vault/entries`, () => HttpResponse.json({ message: 'boom' }, { status: 500 }))
    );

    renderVaultPage();

    expect(await screen.findByText('boom')).toBeInTheDocument();
    // The category header and its "New Entry" action (Manage-level user) must still render.
    expect(screen.getByRole('heading', { name: 'Infra Secrets' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /new entry/i })).toBeInTheDocument();
  });

  describe('reveal secret flow', () => {
    const entrySummary = { id: 100, name: 'Prod DB', systemOrUrl: 'db.prod.internal', username: 'admin' };
    const entryDetail = {
      id: 100,
      name: 'Prod DB',
      systemOrUrl: 'db.prod.internal',
      username: 'admin',
      notes: '',
      creatorName: 'Test Admin',
      createdAt: '2026-01-01T00:00:00Z',
    };

    const openEntryDetail = async () => {
      server.use(
        spacesRootHandler(),
        granteeCountHandler(0),
        http.get(`${API_BASE_URL}/vault/entries`, () => HttpResponse.json([entrySummary])),
        http.get(`${API_BASE_URL}/vault/entries/100`, () => HttpResponse.json(entryDetail))
      );

      const user = userEvent.setup();
      renderVaultPage();
      await user.click(await screen.findByText('Prod DB'));
      await screen.findByRole('button', { name: /reveal secret/i });
      return user;
    };

    it('does not display the plaintext secret before the reveal button is clicked', async () => {
      await openEntryDetail();

      expect(screen.queryByText('sup3r-secret-value')).not.toBeInTheDocument();
      expect(screen.getByRole('button', { name: /reveal secret/i })).toBeInTheDocument();
    });

    it('calls the reveal endpoint and displays the secret only after clicking Reveal', async () => {
      let revealCalled = false;
      server.use(
        http.post(`${API_BASE_URL}/vault/entries/100/reveal`, () => {
          revealCalled = true;
          return HttpResponse.json({ secretValue: 'sup3r-secret-value' });
        })
      );

      const user = await openEntryDetail();
      await user.click(screen.getByRole('button', { name: /reveal secret/i }));

      expect(await screen.findByText('sup3r-secret-value')).toBeInTheDocument();
      expect(revealCalled).toBe(true);
      // The button flips to a "hide" affordance and the reveal trigger is gone once shown.
      expect(screen.queryByRole('button', { name: /reveal secret/i })).not.toBeInTheDocument();
      expect(screen.getByRole('button', { name: /hide/i })).toBeInTheDocument();
    });

    it('reports an error via toast and keeps the secret hidden when the reveal call fails', async () => {
      server.use(
        http.post(`${API_BASE_URL}/vault/entries/100/reveal`, () =>
          HttpResponse.json({ message: 'Not permitted to reveal this secret' }, { status: 403 }))
      );

      const user = await openEntryDetail();
      await user.click(screen.getByRole('button', { name: /reveal secret/i }));

      expect(await screen.findByText('Not permitted to reveal this secret')).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /reveal secret/i })).toBeInTheDocument();
    });
  });

  describe('create entry flow', () => {
    afterEach(() => {
      vi.restoreAllMocks();
    });

    it('submits a POST with the new entry\'s fields and refreshes the list', async () => {
      server.use(
        spacesRootHandler(),
        granteeCountHandler(0),
        http.get(`${API_BASE_URL}/vault/entries`, () => HttpResponse.json([]))
      );

      const user = userEvent.setup();
      renderVaultPage();
      await screen.findByText(/no entries in this category yet/i);

      let postBody = null;
      server.use(
        http.post(`${API_BASE_URL}/vault/entries`, async ({ request }) => {
          postBody = await request.json();
          return HttpResponse.json({ id: 200, ...postBody });
        }),
        http.get(`${API_BASE_URL}/vault/entries`, () =>
          HttpResponse.json([{ id: 200, name: 'New API Key', systemOrUrl: 'stripe.com', username: '' }]))
      );

      await user.click(screen.getByRole('button', { name: /new entry/i }));
      expect(screen.getByRole('heading', { name: /new vault entry/i })).toBeInTheDocument();

      await user.type(screen.getByPlaceholderText('Name'), 'New API Key');
      await user.type(screen.getByPlaceholderText('System / URL'), 'stripe.com');
      await user.type(screen.getByPlaceholderText('Secret'), 'sk_live_abc123');
      await user.click(screen.getByRole('button', { name: /create entry/i }));

      await waitFor(() => expect(postBody).not.toBeNull());
      expect(postBody).toMatchObject({
        name: 'New API Key',
        spaceId: 10,
        systemOrUrl: 'stripe.com',
        secretValue: 'sk_live_abc123',
      });

      expect(await screen.findByText('New API Key')).toBeInTheDocument();
      expect(await screen.findByText('Vault entry added.')).toBeInTheDocument();
      // Modal closes after a successful save.
      expect(screen.queryByRole('heading', { name: /new vault entry/i })).not.toBeInTheDocument();
    });

    it('shows a validation error and does not call the API when the name is left blank', async () => {
      server.use(
        spacesRootHandler(),
        granteeCountHandler(0),
        http.get(`${API_BASE_URL}/vault/entries`, () => HttpResponse.json([]))
      );

      let postCalled = false;
      server.use(http.post(`${API_BASE_URL}/vault/entries`, () => {
        postCalled = true;
        return HttpResponse.json({ id: 201 });
      }));

      const user = userEvent.setup();
      renderVaultPage();
      await screen.findByText(/no entries in this category yet/i);

      await user.click(screen.getByRole('button', { name: /new entry/i }));
      await user.type(screen.getByPlaceholderText('Secret'), 'sk_live_abc123');
      await user.click(screen.getByRole('button', { name: /create entry/i }));

      // The Name input has a native HTML5 `required` attribute, so an empty Name blocks the form's
      // submit event via the browser's own constraint validation before React's onSubmit ever runs
      // (confirmed: this isn't jsdom-specific, real browsers do the same) - the custom red
      // "Name is required." message this form also computes is therefore unreachable through this
      // exact interaction, so it's not what's actually observable here. What genuinely is observable,
      // matching real behavior: the API is never called, and the field reports itself invalid.
      expect(postCalled).toBe(false);
      expect(screen.getByPlaceholderText('Name')).toBeInvalid();
    });
  });
});
