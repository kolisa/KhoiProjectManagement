import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { server, API_BASE_URL } from '../../test/mswServer';
import { ToastProvider } from '../../contexts/ToastContext';
import { ConfirmProvider } from '../../contexts/ConfirmContext';
import ApiService from '../../services/ApiService';
import LibraryPage from './LibraryPage';

// LibraryPage always mounts SpaceTree (src/components/Spaces/SpaceTree.jsx) in its left pane, which
// on mount calls GET /spaces (no query - see SpaceTree.loadRoots calling apiService.getSpaces(null))
// and auto-selects the first root the moment it loads. Every test that wants a folder selected has
// to supply that root through this handler; tests that want the "nothing selected yet" state supply
// an empty array instead so SpaceTree never calls onSelect at all.
const rootSpace = {
  id: 10,
  name: 'Contracts',
  myEffectiveLevel: 'Manage', // Manage >= Write, so both canWrite and canManage affordances render
};

const testUser = { id: 1, name: 'Test User', permissions: [] };

const spacesHandler = (roots = [rootSpace]) =>
  http.get(`${API_BASE_URL}/spaces`, () => HttpResponse.json(roots));

const renderLibraryPage = (props = {}) => {
  const apiService = new ApiService();
  return render(
    <ToastProvider>
      <ConfirmProvider>
        <LibraryPage apiService={apiService} user={testUser} teamMembers={[]} {...props} />
      </ConfirmProvider>
    </ToastProvider>
  );
};

describe('LibraryPage', () => {
  it('shows the "select a folder" placeholder when no space is auto-selected', async () => {
    server.use(spacesHandler([]));

    renderLibraryPage();

    expect(await screen.findByText(/no spaces available to you yet/i)).toBeInTheDocument();
    expect(screen.getByText(/select a folder on the left to view its files/i)).toBeInTheDocument();
  });

  it('auto-selects the first root folder and renders its files', async () => {
    server.use(
      spacesHandler(),
      http.get(`${API_BASE_URL}/library/files`, () => HttpResponse.json([
        { id: 1, fileName: 'Report.pdf', currentVersionNumber: 2, fileSize: 2048, creatorName: 'Alice' },
      ]))
    );

    renderLibraryPage();

    expect(await screen.findByRole('heading', { name: /contracts/i, level: 3 })).toBeInTheDocument();
    expect(await screen.findByText('Report.pdf')).toBeInTheDocument();
    expect(screen.getByText(/v2 · 2\.0 KB · Alice/i)).toBeInTheDocument();
    expect(screen.getByText('1 file')).toBeInTheDocument();
  });

  it('shows the empty state when the selected folder has no files', async () => {
    server.use(
      spacesHandler(),
      http.get(`${API_BASE_URL}/library/files`, () => HttpResponse.json([]))
    );

    renderLibraryPage();

    expect(await screen.findByText(/no files in this folder yet/i)).toBeInTheDocument();
    expect(screen.getByText('0 files')).toBeInTheDocument();
  });

  it('shows an error message when loading files fails, without crashing the folder pane', async () => {
    server.use(
      spacesHandler(),
      http.get(`${API_BASE_URL}/library/files`, () => HttpResponse.json({ message: 'Could not reach the library service.' }, { status: 500 }))
    );

    renderLibraryPage();

    expect(await screen.findByText(/could not reach the library service/i)).toBeInTheDocument();
    // The rest of the pane (folder heading, upload affordance) must still render, not just the error.
    expect(screen.getByRole('heading', { name: /contracts/i, level: 3 })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /upload file/i })).toBeInTheDocument();
  });

  it('uploads a new file via the hidden file input and refreshes the list, with a success toast', async () => {
    let uploadPosted = false;
    let filesCallCount = 0;

    server.use(
      spacesHandler(),
      http.get(`${API_BASE_URL}/library/files`, () => {
        filesCallCount += 1;
        // Second call (post-upload refresh) reflects the newly uploaded file; first call is the
        // initial empty listing.
        if (filesCallCount > 1) {
          return HttpResponse.json([
            { id: 2, fileName: 'Budget.xlsx', currentVersionNumber: 1, fileSize: 4096, creatorName: 'Test User' },
          ]);
        }
        return HttpResponse.json([]);
      }),
      // Deliberately never reads the request body (not request.formData(), not request.text()) - a
      // multipart body built from a jsdom File (what production code's `new FormData()` + a real
      // <input type="file"> selection produce here) never finishes streaming through Node's native
      // fetch, the thing MSW actually intercepts in this jsdom test environment - any attempt to read
      // it hangs the request indefinitely, a confirmed jsdom/undici cross-realm incompatibility, not
      // a bug in LibraryPage or ApiService. What actually happened is instead verified through what's
      // observable without touching the body: the toast text (built client-side from the real
      // selected File's .name) and the refetched list.
      http.post(`${API_BASE_URL}/library/files`, () => {
        uploadPosted = true;
        return HttpResponse.json({ id: 2, fileName: 'Budget.xlsx' }, { status: 201 });
      })
    );

    const user = userEvent.setup();
    const { container } = renderLibraryPage();

    await screen.findByText(/no files in this folder yet/i);

    // The visible "Upload File" button just clicks a hidden <input type="file"> ref (see
    // LibraryPage.jsx's uploadInputRef) - RTL/user-event can drive that hidden input directly with a
    // real in-memory File, no disk I/O or actually clicking the button required.
    const fileInput = container.querySelector('input[type="file"]');
    const file = new File(['budget contents'], 'Budget.xlsx', { type: 'application/vnd.ms-excel' });
    await user.upload(fileInput, file);

    await waitFor(() => expect(uploadPosted).toBe(true));

    expect(await screen.findByText('Budget.xlsx')).toBeInTheDocument();
    expect(await screen.findByText('"Budget.xlsx" uploaded.')).toBeInTheDocument();
  });

  it('shows an error toast when the upload request fails', async () => {
    server.use(
      spacesHandler(),
      http.get(`${API_BASE_URL}/library/files`, () => HttpResponse.json([])),
      http.post(`${API_BASE_URL}/library/files`, () => HttpResponse.json({ message: 'File type not allowed.' }, { status: 400 }))
    );

    const user = userEvent.setup();
    const { container } = renderLibraryPage();

    await screen.findByText(/no files in this folder yet/i);

    const fileInput = container.querySelector('input[type="file"]');
    const file = new File(['x'], 'virus.exe', { type: 'application/octet-stream' });
    await user.upload(fileInput, file);

    expect(await screen.findByText(/file type not allowed/i)).toBeInTheDocument();
    // The failed upload must not be treated as if a file were added.
    expect(screen.getByText(/no files in this folder yet/i)).toBeInTheDocument();
  });

  it('expands version history for a file and lists its past versions', async () => {
    server.use(
      spacesHandler(),
      http.get(`${API_BASE_URL}/library/files`, () => HttpResponse.json([
        { id: 5, fileName: 'Policy.docx', currentVersionNumber: 3, fileSize: 1500, creatorName: 'Bob' },
      ])),
      http.get(`${API_BASE_URL}/library/files/5/versions`, () => HttpResponse.json([
        { versionNumber: 3, uploadedByName: 'Bob', fileSize: 1500, comment: 'Final wording', uploadedAt: '2026-08-20T10:00:00Z' },
        { versionNumber: 2, uploadedByName: 'Alice', fileSize: 1400, comment: null, uploadedAt: '2026-08-10T10:00:00Z' },
        { versionNumber: 1, uploadedByName: 'Alice', fileSize: 1200, comment: 'Initial draft', uploadedAt: '2026-08-01T10:00:00Z' },
      ]))
    );

    const user = userEvent.setup();
    renderLibraryPage();

    await screen.findByText('Policy.docx');
    await user.click(screen.getByRole('button', { name: /version history/i }));

    expect(await screen.findByText(/final wording/i)).toBeInTheDocument();
    expect(screen.getByText('v3')).toBeInTheDocument();
    expect(screen.getByText('v2')).toBeInTheDocument();
    expect(screen.getByText('v1')).toBeInTheDocument();
    expect(screen.getByText(/initial draft/i)).toBeInTheDocument();

    // Toggling the same button again collapses it.
    await user.click(screen.getByRole('button', { name: /version history/i }));
    await waitFor(() => expect(screen.queryByText(/final wording/i)).not.toBeInTheDocument());
  });

  it('hides the "New root folder" button when the user lacks spaces.manage', async () => {
    server.use(spacesHandler([]));

    renderLibraryPage({ user: { id: 1, name: 'No Perms', permissions: [] } });

    await screen.findByText(/no spaces available to you yet/i);
    expect(screen.queryByRole('button', { name: /new root folder/i })).not.toBeInTheDocument();
  });

  it('shows the "New root folder" button when the user has spaces.manage', async () => {
    server.use(spacesHandler([]));

    renderLibraryPage({ user: { id: 1, name: 'Admin', permissions: ['spaces.manage'] } });

    await screen.findByText(/no spaces available to you yet/i);
    expect(screen.getByRole('button', { name: /new root folder/i })).toBeInTheDocument();
  });
});
