import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { beforeEach, describe, expect, it } from 'vitest';
import { server, API_BASE_URL } from '../../test/mswServer';
import { ToastProvider } from '../../contexts/ToastContext';
import { ConfirmProvider } from '../../contexts/ConfirmContext';
import ApiService from '../../services/ApiService';
import IdeasPage from './IdeasPage';

// IdeasPage is a plain, separately-exported container (unlike the Wiki/Vault-style tabs that only
// render inside App.jsx's AuthGuard) - it can be rendered directly with a real ApiService instance
// (so MSW intercepts its actual fetch() calls, same as App.test.jsx) wrapped in ToastProvider (since
// handleCreate calls useToast().success(...) on a successful submit) and ConfirmProvider (IdeaDetail's
// delete-attachment/convert-to-project actions use useConfirm() instead of window.confirm()).

const managerUser = { id: 1, name: 'Manager Mel', permissions: ['ideas.manage'] };
const plainUser = { id: 2, name: 'Contributor Cam', permissions: [] };

const daysAgoIso = (days) => new Date(Date.now() - days * 86_400_000).toISOString();

const sampleIdeas = [
  {
    id: 1, title: 'Add dark mode', submitterName: 'Alice Admin', status: 'Submitted',
    commentCount: 2, createdAt: daysAgoIso(2),
  },
  {
    id: 2, title: 'Automate invoicing', submitterName: 'Bob Builder', status: 'UnderReview',
    commentCount: 0, createdAt: daysAgoIso(0),
  },
  {
    id: 3, title: 'Migrate to Postgres 17', submitterName: 'Cara Coder', status: 'Approved',
    commentCount: 5, createdAt: daysAgoIso(1),
  },
];

const renderIdeasPage = ({ apiService = new ApiService(), user = managerUser } = {}) =>
  render(
    <ToastProvider>
      <ConfirmProvider>
        <IdeasPage apiService={apiService} user={user} />
      </ConfirmProvider>
    </ToastProvider>
  );

// Locates a Kanban column's container by its header label. Scoped to the <h3> header specifically -
// a bare screen.getByText(label) also matches that status's StatusBadge span on any idea card, and
// (once an idea's detail panel is open) the status <select>'s same-labeled <option>.
const getColumn = (label) => screen.getByText(label, { selector: 'h3' }).closest('.w-72');

describe('IdeasPage', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it('shows a loading placeholder before the ideas list resolves', async () => {
    server.use(http.get(`${API_BASE_URL}/ideas`, () => HttpResponse.json(sampleIdeas)));

    renderIdeasPage();

    expect(screen.getByText(/loading/i)).toBeInTheDocument();
    await screen.findByText('Add dark mode');
  });

  it('renders returned ideas grouped into their status columns', async () => {
    server.use(http.get(`${API_BASE_URL}/ideas`, () => HttpResponse.json(sampleIdeas)));

    renderIdeasPage();

    expect(await screen.findByText(/3 ideas/i)).toBeInTheDocument();

    const submittedColumn = getColumn('Submitted');
    expect(within(submittedColumn).getByText('Add dark mode')).toBeInTheDocument();
    expect(within(submittedColumn).getByText('Alice Admin')).toBeInTheDocument();
    expect(within(submittedColumn).getByText('2')).toBeInTheDocument(); // comment count badge

    const underReviewColumn = getColumn('Under Review');
    expect(within(underReviewColumn).getByText('Automate invoicing')).toBeInTheDocument();

    const approvedColumn = getColumn('Approved');
    expect(within(approvedColumn).getByText('Migrate to Postgres 17')).toBeInTheDocument();

    // Columns with nothing in them render the empty placeholder, and each idea appears in exactly
    // its own status column, not any other.
    const rejectedColumn = getColumn('Rejected');
    expect(within(rejectedColumn).getByText(/no ideas here/i)).toBeInTheDocument();
    expect(within(rejectedColumn).queryByText('Add dark mode')).not.toBeInTheDocument();
  });

  it('shows the empty state when the API returns no ideas', async () => {
    server.use(http.get(`${API_BASE_URL}/ideas`, () => HttpResponse.json([])));

    renderIdeasPage();

    // ideas is set to [] (truthy), not left null, so the header switches to the "0 ideas" count
    // rather than staying on the pre-load "Share ideas..." placeholder copy.
    expect(await screen.findByText(/0 ideas/i)).toBeInTheDocument();
    // All five columns render their own "No ideas here" placeholder.
    expect(await screen.findAllByText(/no ideas here/i)).toHaveLength(5);
  });

  it('shows an error message without crashing when the request fails', async () => {
    server.use(http.get(`${API_BASE_URL}/ideas`, () => HttpResponse.json({ message: 'Could not load ideas' }, { status: 500 })));

    renderIdeasPage();

    expect(await screen.findByText('Could not load ideas')).toBeInTheDocument();
    // A failed load settles into an empty board rather than staying stuck on "Loading..." forever
    // underneath the error message (fixed 2026-08-28 - previously ideas stayed null on error).
    expect(screen.queryByText(/loading/i)).not.toBeInTheDocument();
    expect(screen.getAllByText('No ideas here').length).toBeGreaterThan(0);
    expect(screen.getByRole('button', { name: /new idea/i })).toBeInTheDocument();
  });

  it('shows a "waiting on your review" count only for a user with ideas.manage permission', async () => {
    server.use(http.get(`${API_BASE_URL}/ideas`, () => HttpResponse.json(sampleIdeas)));

    renderIdeasPage({ user: managerUser });

    // Submitted + UnderReview = 2 of the 3 sample ideas.
    expect(await screen.findByText(/2 waiting on your review/i)).toBeInTheDocument();
  });

  it('hides the "waiting on your review" count for a user without ideas.manage permission', async () => {
    server.use(http.get(`${API_BASE_URL}/ideas`, () => HttpResponse.json(sampleIdeas)));

    renderIdeasPage({ user: plainUser });

    await screen.findByText(/3 ideas/i);
    expect(screen.queryByText(/waiting on your review/i)).not.toBeInTheDocument();
  });

  it('submits a new idea with a POST containing the typed title/description and refreshes the list', async () => {
    server.use(http.get(`${API_BASE_URL}/ideas`, () => HttpResponse.json(sampleIdeas)));
    const user = userEvent.setup();
    renderIdeasPage();
    await screen.findByText('Add dark mode');

    let postBody = null;
    const createdIdea = {
      id: 4, title: 'Dark mode for the mobile app', description: 'Match the desktop theme toggle.',
      status: 'Submitted', submittedBy: 1, submitterName: 'Manager Mel', commentCount: 0, createdAt: daysAgoIso(0),
    };
    server.use(
      http.post(`${API_BASE_URL}/ideas`, async ({ request }) => {
        postBody = await request.json();
        return HttpResponse.json(createdIdea);
      }),
      http.get(`${API_BASE_URL}/ideas`, () => HttpResponse.json([...sampleIdeas, createdIdea])),
      // The board auto-opens the new idea's detail modal after creation (setSelectedId(created.id)),
      // which mounts IdeaDetail and fires its own load() - out of scope to assert on, but its calls
      // must be mocked or the shared MSW server (onUnhandledRequest: 'error') fails the test.
      http.get(`${API_BASE_URL}/ideas/4`, () => HttpResponse.json(createdIdea)),
      http.get(`${API_BASE_URL}/ideas/4/comments`, () => HttpResponse.json([])),
      http.get(`${API_BASE_URL}/ideas/4/attachments`, () => HttpResponse.json([]))
    );

    await user.click(screen.getByRole('button', { name: /new idea/i }));
    expect(screen.getByRole('heading', { name: /new idea/i })).toBeInTheDocument();

    await user.type(screen.getByPlaceholderText('Title'), 'Dark mode for the mobile app');
    await user.type(screen.getByPlaceholderText(/describe your idea/i), 'Match the desktop theme toggle.');
    await user.click(screen.getByRole('button', { name: /submit idea/i }));

    await waitFor(() => expect(postBody).not.toBeNull());
    expect(postBody).toEqual({ title: 'Dark mode for the mobile app', description: 'Match the desktop theme toggle.' });

    // Modal closes and the refreshed list (now 4 ideas) shows the new one in its column.
    expect(await screen.findByText(/4 ideas/i)).toBeInTheDocument();
    const submittedColumn = getColumn('Submitted');
    expect(within(submittedColumn).getByText('Dark mode for the mobile app')).toBeInTheDocument();
    expect(await screen.findByText('Idea submitted.')).toBeInTheDocument();
  });

  it('keeps the new-idea modal open and shows the server error when creation fails', async () => {
    server.use(http.get(`${API_BASE_URL}/ideas`, () => HttpResponse.json(sampleIdeas)));
    const user = userEvent.setup();
    renderIdeasPage();
    await screen.findByText('Add dark mode');

    server.use(http.post(`${API_BASE_URL}/ideas`, () => HttpResponse.json({ message: 'Title already in use' }, { status: 400 })));

    await user.click(screen.getByRole('button', { name: /new idea/i }));
    await user.type(screen.getByPlaceholderText('Title'), 'Duplicate title');
    await user.type(screen.getByPlaceholderText(/describe your idea/i), 'Some description here.');
    await user.click(screen.getByRole('button', { name: /submit idea/i }));

    expect(await screen.findByText('Title already in use')).toBeInTheDocument();
    // The modal itself stays open on failure so the user doesn't lose what they typed.
    expect(screen.getByRole('heading', { name: /new idea/i })).toBeInTheDocument();
    expect(screen.getByDisplayValue('Duplicate title')).toBeInTheDocument();
  });
});
