import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { server, API_BASE_URL } from './test/mswServer';
import App from './App';

const dashboardHandlers = () => [
  http.get(`${API_BASE_URL}/dashboard/statistics`, () => HttpResponse.json({
    totalProjects: 0, activeProjects: 0, totalTasks: 0, completedTasks: 0,
    inProgressTasks: 0, todoTasks: 0, overdueTasks: 0, completionRate: 0,
  })),
  http.get(`${API_BASE_URL}/tasks`, () => HttpResponse.json([])),
  http.get(`${API_BASE_URL}/notifications`, () => HttpResponse.json([])),
  http.get(`${API_BASE_URL}/dashboard/widgets/my-preferences`, () => HttpResponse.json([])),
  http.get(`${API_BASE_URL}/timesheets`, () => HttpResponse.json([])),
  http.get(`${API_BASE_URL}/dashboard/weekly-completion`, () => HttpResponse.json([0, 0, 0, 0, 0, 0, 0])),
  http.get(`${API_BASE_URL}/dashboard/activity`, () => HttpResponse.json([])),
];

// App.jsx's real LoginForm/AuthGuard aren't separately exported (see CLAUDE.md's frontend-structure
// note) - they're only reachable by rendering the default-exported App, which is what these tests do
// rather than refactoring App.jsx to extract them just for testability.
describe('App (LoginForm / AuthGuard)', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it('renders the login form when no user is authenticated', async () => {
    render(<App />);

    expect(await screen.findByText(/sign in to khoi pro/i)).toBeInTheDocument();
    expect(screen.getByPlaceholderText(/email address/i)).toBeInTheDocument();
    expect(screen.getByPlaceholderText(/^password$/i)).toBeInTheDocument();
  });

  it('shows an error and stays on the login form when credentials are wrong', async () => {
    server.use(
      http.post(`${API_BASE_URL}/auth/login`, () =>
        HttpResponse.json({ message: 'Invalid email or password' }, { status: 401 }))
    );
    const user = userEvent.setup();
    render(<App />);
    await screen.findByText(/sign in to khoi pro/i);

    await user.type(screen.getByPlaceholderText(/email address/i), 'wrong@khoitech.africa');
    await user.type(screen.getByPlaceholderText(/^password$/i), 'wrong-password');
    await user.click(screen.getByRole('button', { name: /sign in/i }));

    expect(await screen.findByText(/invalid email or password/i)).toBeInTheDocument();
    expect(screen.getByPlaceholderText(/email address/i)).toBeInTheDocument();
  });

  it('shows a "couldn\'t reach the server" error (not "wrong password") when the request never reaches the API', async () => {
    // Regression test: a CORS block, a dead connection, or a timeout must not be misreported as
    // "Invalid email or password" - that sends people chasing the wrong problem entirely (this exact
    // confusion is what prompted this fix). HttpResponse.error() simulates a real network-level
    // failure, same as an actual CORS-blocked or unreachable request would produce.
    server.use(http.post(`${API_BASE_URL}/auth/login`, () => HttpResponse.error()));
    const user = userEvent.setup();
    render(<App />);
    await screen.findByText(/sign in to khoi pro/i);

    await user.type(screen.getByPlaceholderText(/email address/i), 'admin@khoitech.africa');
    await user.type(screen.getByPlaceholderText(/^password$/i), 'admin123');
    await user.click(screen.getByRole('button', { name: /sign in/i }));

    expect(await screen.findByText(/couldn't reach the server/i)).toBeInTheDocument();
    expect(screen.queryByText(/invalid email or password/i)).not.toBeInTheDocument();
  });

  it('reaches the authenticated dashboard shell after a successful login', async () => {
    const loginResponse = {
      token: 'fake-jwt-token',
      refreshToken: 'fake-refresh-token',
      user: { id: 1, name: 'Test Admin', email: 'admin@khoitech.africa', role: 'admin', position: 'Owner', isActive: true },
      permissions: ['dashboard.view'],
      expiresAt: new Date(Date.now() + 900_000).toISOString(),
    };

    server.use(
      http.post(`${API_BASE_URL}/auth/login`, () => HttpResponse.json(loginResponse)),
      ...dashboardHandlers()
    );

    const user = userEvent.setup();
    render(<App />);
    await screen.findByText(/sign in to khoi pro/i);

    await user.type(screen.getByPlaceholderText(/email address/i), 'admin@khoitech.africa');
    await user.type(screen.getByPlaceholderText(/^password$/i), 'admin123');
    await user.click(screen.getByRole('button', { name: /sign in/i }));

    await waitFor(() => expect(screen.getByRole('heading', { name: /good (morning|afternoon|evening)/i })).toBeInTheDocument());
    // Login form must be gone, not just the dashboard heading present alongside it.
    expect(screen.queryByText(/sign in to khoi pro/i)).not.toBeInTheDocument();
  });
});

// Covers section 28-30 of the test brief (loading/empty/error states for an API-backed view) against
// the Projects tab - the closest thing this single-file dashboard has to a standalone "list page".
describe('App > Projects tab data loading', () => {
  const loginResponse = {
    token: 'fake-jwt-token',
    refreshToken: 'fake-refresh-token',
    user: { id: 1, name: 'Test Admin', email: 'admin@khoitech.africa', role: 'admin', position: 'Owner', isActive: true },
    permissions: ['dashboard.view', 'projects.create'],
    expiresAt: new Date(Date.now() + 900_000).toISOString(),
  };

  const mockDashboardLoad = () => {
    server.use(...dashboardHandlers());
  };

  const loginAndOpenProjectsTab = async () => {
    server.use(http.post(`${API_BASE_URL}/auth/login`, () => HttpResponse.json(loginResponse)));
    mockDashboardLoad();

    const user = userEvent.setup();
    render(<App />);
    await screen.findByText(/sign in to khoi pro/i);
    await user.type(screen.getByPlaceholderText(/email address/i), 'admin@khoitech.africa');
    await user.type(screen.getByPlaceholderText(/^password$/i), 'admin123');
    await user.click(screen.getByRole('button', { name: /sign in/i }));
    await screen.findByRole('heading', { name: /good (morning|afternoon|evening)/i });

    await user.click(screen.getByRole('button', { name: /^projects$/i }));
    return user;
  };

  beforeEach(() => {
    localStorage.clear();
  });

  it('shows the empty state when the API returns no projects', async () => {
    server.use(http.get(`${API_BASE_URL}/projects`, () => HttpResponse.json([])));

    await loginAndOpenProjectsTab();

    expect(await screen.findByText(/no projects found/i)).toBeInTheDocument();
  });

  it('renders returned projects by name', async () => {
    server.use(http.get(`${API_BASE_URL}/projects`, () => HttpResponse.json([
      {
        id: 1, name: 'Apollo Migration', description: 'Move to Postgres', status: 'active', priority: 'high',
        startDate: '2026-01-01', endDate: '2026-03-01', creatorName: 'Test Admin', tags: [], teamMembers: [],
        taskCount: 0, completedTaskCount: 0,
      },
    ])));

    await loginAndOpenProjectsTab();

    expect(await screen.findByRole('heading', { name: /apollo migration/i })).toBeInTheDocument();
    expect(screen.queryByText(/no projects found/i)).not.toBeInTheDocument();
  });

  it('shows an error message with a retry option when the request fails, without crashing the page', async () => {
    server.use(http.get(`${API_BASE_URL}/projects`, () => HttpResponse.json({ message: 'boom' }, { status: 500 })));

    await loginAndOpenProjectsTab();

    expect(await screen.findByText(/error:/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /try again/i })).toBeInTheDocument();
    // The rest of the shell (nav) must still be intact - a crash would take it down too.
    expect(screen.getByRole('button', { name: /^dashboard$/i })).toBeInTheDocument();
  });
});

// Regression coverage for the "edit project" gap this pass closed - the Edit3 pencil icon on a
// project card previously had no onClick at all (see openEditProject/handleAddProject in App.jsx).
describe('App > Edit project', () => {
  const loginResponse = {
    token: 'fake-jwt-token',
    refreshToken: 'fake-refresh-token',
    user: { id: 1, name: 'Test Admin', email: 'admin@khoitech.africa', role: 'admin', position: 'Owner', isActive: true },
    permissions: ['dashboard.view', 'projects.edit'],
    expiresAt: new Date(Date.now() + 900_000).toISOString(),
  };

  const existingProject = {
    id: 7, name: 'Apollo Migration', description: 'Move to Postgres', status: 'active', priority: 'high',
    startDate: '2026-01-01T00:00:00', endDate: '2026-03-01T00:00:00', creatorName: 'Test Admin',
    tags: ['infra'], teamMembers: [], taskCount: 0, completedTaskCount: 0,
  };

  beforeEach(() => {
    localStorage.clear();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  const loginAndOpenProjectsTab = async () => {
    server.use(
      http.post(`${API_BASE_URL}/auth/login`, () => HttpResponse.json(loginResponse)),
      ...dashboardHandlers(),
      http.get(`${API_BASE_URL}/projects`, () => HttpResponse.json([existingProject]))
    );

    const user = userEvent.setup();
    render(<App />);
    await screen.findByText(/sign in to khoi pro/i);
    await user.type(screen.getByPlaceholderText(/email address/i), 'admin@khoitech.africa');
    await user.type(screen.getByPlaceholderText(/^password$/i), 'admin123');
    await user.click(screen.getByRole('button', { name: /sign in/i }));
    await screen.findByRole('heading', { name: /good (morning|afternoon|evening)/i });

    await user.click(screen.getByRole('button', { name: /^projects$/i }));
    await screen.findByRole('heading', { name: /apollo migration/i });
    return user;
  };

  // The Edit3 icon button has no accessible name; this test's user only holds projects.edit (not
  // projects.delete), so the card's action area renders exactly one (unnamed) button - scoped to the
  // card via `within` so this doesn't collide with other icon-only buttons elsewhere in the header.
  const getEditButtonForCard = () => {
    const card = screen.getByRole('heading', { name: /apollo migration/i }).closest('.bg-white');
    return within(card).getByRole('button');
  };

  it('opens the edit modal pre-filled with the project\'s current values', async () => {
    const user = await loginAndOpenProjectsTab();

    await user.click(getEditButtonForCard());

    expect(screen.getByRole('heading', { name: /edit project/i })).toBeInTheDocument();
    expect(screen.getByDisplayValue('Apollo Migration')).toBeInTheDocument();
    expect(screen.getByDisplayValue('Move to Postgres')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /save changes/i })).toBeInTheDocument();
  });

  it('submits a PUT with the edited values and refreshes the list', async () => {
    const user = await loginAndOpenProjectsTab();
    let putBody = null;

    server.use(
      http.put(`${API_BASE_URL}/projects/7`, async ({ request }) => {
        putBody = await request.json();
        return new HttpResponse(null, { status: 204 });
      }),
      http.get(`${API_BASE_URL}/projects`, () => HttpResponse.json([{ ...existingProject, name: 'Apollo Migration v2' }]))
    );

    await user.click(getEditButtonForCard());
    const nameInput = screen.getByDisplayValue('Apollo Migration');
    await user.clear(nameInput);
    await user.type(nameInput, 'Apollo Migration v2');
    await user.click(screen.getByRole('button', { name: /save changes/i }));

    await waitFor(() => expect(putBody).not.toBeNull());
    expect(putBody.name).toBe('Apollo Migration v2');
    expect(putBody.status).toBe('active'); // status carried through, not silently reset

    expect(await screen.findByRole('heading', { name: /apollo migration v2/i })).toBeInTheDocument();
    expect(await screen.findByText('Project updated successfully!')).toBeInTheDocument();
  });
});
