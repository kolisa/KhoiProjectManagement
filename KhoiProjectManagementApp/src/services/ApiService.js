// src/services/ApiService.js
const API_BASE_URL = import.meta.env.VITE_API_URL || 'https://localhost:7148/api';

// Plain JSON requests get a fairly generous bound - long enough to tolerate a genuinely slow (not
// just laggy) connection without the caller's loading spinner spinning forever with no way out.
// File transfers (requestMultipart/downloadBlob) get their own, much longer bound below - a slow
// connection needs *more* time to move bytes, not less.
const DEFAULT_TIMEOUT_MS = 20_000;
const UPLOAD_TIMEOUT_MS = 120_000;

// Thrown for "never got a response at all" (timed out, offline, DNS/connection failure) - distinct
// from a normal HTTP error response, so callers/UI can tell "the server said no" apart from
// "we couldn't reach the server," which matters for what to tell the user and whether retrying makes
// sense at all.
export class NetworkError extends Error {
  constructor(message, { cause } = {}) {
    super(message);
    this.name = 'NetworkError';
    this.isNetworkError = true;
    if (cause) this.cause = cause;
  }
}

const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

// fetchWithTimeout above already converts every network-level failure (timeout, offline, DNS/
// connection failure) into a NetworkError before it reaches here - a real HTTP error response
// (thrown just below as a plain Error) is never retried.
const isTransientFetchFailure = (error) => error instanceof NetworkError;

class ApiService {
  constructor() {
    this.token = localStorage.getItem('jwt_token') || null;
    this.refreshToken = localStorage.getItem('refresh_token') || null;
  }

  async fetchWithTimeout(url, config, timeoutMs) {
    const controller = new AbortController();
    const timeoutId = setTimeout(() => controller.abort(), timeoutMs);
    try {
      return await fetch(url, { ...config, signal: controller.signal });
    } catch (error) {
      if (error.name === 'AbortError') {
        throw new NetworkError('The request timed out - your connection may be slow or offline.', { cause: error });
      }
      // A plain fetch TypeError ("Failed to fetch"/"NetworkError when attempting to fetch resource")
      // means the request never reached the server at all (offline, DNS failure, connection refused).
      if (error.name === 'TypeError') {
        throw new NetworkError('Could not reach the server - check your connection.', { cause: error });
      }
      throw error;
    } finally {
      clearTimeout(timeoutId);
    }
  }

  async request(endpoint, options = {}, { timeoutMs = DEFAULT_TIMEOUT_MS } = {}) {
    const url = `${API_BASE_URL}${endpoint}`;
    const config = {
      headers: {
        'Content-Type': 'application/json',
        ...(this.token && { Authorization: `Bearer ${this.token}` }),
        ...options.headers,
      },
      ...options,
    };
    // GET is idempotent - safe to retry once, transparently, on a network-level failure (not on a
    // real HTTP error response) before making the caller deal with it. Non-GET requests never retry
    // here, since a timed-out POST/PUT/DELETE may or may not have actually been applied server-side.
    const isRetryable = !config.method || config.method.toUpperCase() === 'GET';

    for (let attempt = 0; ; attempt++) {
      try {
        const response = await this.fetchWithTimeout(url, config, timeoutMs);

        if (response.status === 401) {
          // A 401 from /auth/login itself just means "wrong credentials" - not an expired session -
          // so it must reach LoginForm's own catch block and show an inline error, not trigger the
          // full-page reload below (which was silently discarding that error before the user ever saw
          // it - found via an E2E test against a real browser; jsdom stubs location.reload() as a
          // no-op, which is why component-level tests never caught this).
          if (endpoint === '/auth/login') {
            throw new Error('Invalid email or password');
          }

          this.token = null;
          localStorage.removeItem('jwt_token');
          window.location.reload();
          return null;
        }

        if (!response.ok) {
          throw new Error(`API Error: ${response.status} ${response.statusText}`);
        }

        const contentType = response.headers.get('content-type');
        if (contentType && contentType.includes('application/json')) {
          return await response.json();
        }

        return response;
      } catch (error) {
        if (isRetryable && attempt === 0 && isTransientFetchFailure(error)) {
          await sleep(1500);
          continue;
        }
        console.error('API Request failed:', error);
        throw error;
      }
    }
  }

  // Authentication
  async login(email, password) {
    const response = await this.request('/auth/login', {
      method: 'POST',
      body: JSON.stringify({ email, password }),
    });

    if (response?.token) {
      this.token = response.token;
      localStorage.setItem('jwt_token', response.token);
    }
    if (response?.refreshToken) {
      this.refreshToken = response.refreshToken;
      localStorage.setItem('refresh_token', response.refreshToken);
    }

    return response;
  }

  async register(userData) {
    return await this.request('/auth/register', {
      method: 'POST',
      body: JSON.stringify(userData),
    });
  }

  async forgotPassword(email) {
    return await this.request('/auth/forgot-password', {
      method: 'POST',
      body: JSON.stringify({ email }),
    });
  }

  async resetPassword(token, newPassword) {
    return await this.request('/auth/reset-password', {
      method: 'POST',
      body: JSON.stringify({ token, newPassword }),
    });
  }

  // Returns { user, permissions } straight from the current access token's claims - call this on app
  // load instead of decoding the JWT client-side, since permissions live in the token as claims but
  // aren't meant to be parsed out of it directly.
  async getMe() {
    return await this.request('/auth/me');
  }

  // Projects
  async getProjects() {
    return await this.request('/projects');
  }

  async createProject(projectData) {
    return await this.request('/projects', {
      method: 'POST',
      body: JSON.stringify(projectData),
    });
  }

  async updateProject(id, projectData) {
    return await this.request(`/projects/${id}`, {
      method: 'PUT',
      body: JSON.stringify(projectData),
    });
  }

  async deleteProject(id) {
    return await this.request(`/projects/${id}`, {
      method: 'DELETE',
    });
  }

  // Tasks
  async getTasks(filter = {}) {
    const queryParams = new URLSearchParams();
    Object.keys(filter).forEach(key => {
      if (filter[key] !== null && filter[key] !== undefined && filter[key] !== '') {
        queryParams.append(key, filter[key]);
      }
    });
    const queryString = queryParams.toString();
    return await this.request(`/tasks${queryString ? `?${queryString}` : ''}`);
  }

  async createTask(taskData) {
    return await this.request('/tasks', {
      method: 'POST',
      body: JSON.stringify(taskData),
    });
  }

  async updateTaskStatus(id, status) {
    return await this.request(`/tasks/${id}/status`, {
      method: 'PUT',
      body: JSON.stringify(status),
    });
  }

  async deleteTask(id) {
    return await this.request(`/tasks/${id}`, {
      method: 'DELETE',
    });
  }

  // Users
  async getUsers() {
    return await this.request('/users');
  }

  async createUser(userData) {
    return await this.request('/users', {
      method: 'POST',
      body: JSON.stringify(userData),
    });
  }

  // Reports
  async getProjectSummaryReport() {
    return await this.request('/reports/project-summary');
  }

  async getTeamPerformanceReport() {
    return await this.request('/reports/team-performance');
  }

  async getOverdueTasksReport() {
    return await this.request('/reports/overdue-tasks');
  }

  // Notifications
  async getNotifications() {
    return await this.request('/notifications');
  }

  async markNotificationAsRead(id) {
    return await this.request(`/notifications/${id}/read`, {
      method: 'PUT',
    });
  }

  async getNotificationPreferences() {
    return await this.request('/notifications/preferences');
  }

  async setNotificationPreferences(updates) {
    return await this.request('/notifications/preferences', {
      method: 'PUT',
      body: JSON.stringify(updates),
    });
  }

  // Dashboard
  async getDashboardStats() {
    return await this.request('/dashboard/statistics');
  }

  async getDashboardWidgetCatalog() {
    return await this.request('/dashboard/widgets/catalog');
  }

  async setDashboardWidgetAllowlist(updates) {
    return await this.request('/dashboard/widgets/allowlist', {
      method: 'PUT',
      body: JSON.stringify(updates),
    });
  }

  async getMyDashboardWidgetPreferences() {
    return await this.request('/dashboard/widgets/my-preferences');
  }

  async setMyDashboardWidgetPreferences(updates) {
    return await this.request('/dashboard/widgets/my-preferences', {
      method: 'PUT',
      body: JSON.stringify(updates),
    });
  }

  // Spaces (generic hierarchical containers - shared by the vault and wiki below)
  async getSpaces(parentSpaceId) {
    const query = parentSpaceId !== undefined && parentSpaceId !== null ? `?parentSpaceId=${parentSpaceId}` : '';
    return await this.request(`/spaces${query}`);
  }

  async getSpace(id) {
    return await this.request(`/spaces/${id}`);
  }

  async createSpace(spaceData) {
    return await this.request('/spaces', {
      method: 'POST',
      body: JSON.stringify(spaceData),
    });
  }

  async updateSpace(id, spaceData) {
    return await this.request(`/spaces/${id}`, {
      method: 'PUT',
      body: JSON.stringify(spaceData),
    });
  }

  async deleteSpace(id) {
    return await this.request(`/spaces/${id}`, {
      method: 'DELETE',
    });
  }

  async getSpacePermissions(id) {
    return await this.request(`/spaces/${id}/permissions`);
  }

  async setSpacePermissions(id, grants) {
    return await this.request(`/spaces/${id}/permissions`, {
      method: 'PUT',
      body: JSON.stringify(grants),
    });
  }

  // Vault
  async getVaultEntries(spaceId) {
    return await this.request(`/vault/entries?spaceId=${spaceId}`);
  }

  async getVaultEntry(id) {
    return await this.request(`/vault/entries/${id}`);
  }

  async revealVaultSecret(id) {
    return await this.request(`/vault/entries/${id}/reveal`, {
      method: 'POST',
    });
  }

  async createVaultEntry(entryData) {
    return await this.request('/vault/entries', {
      method: 'POST',
      body: JSON.stringify(entryData),
    });
  }

  async updateVaultEntry(id, entryData) {
    return await this.request(`/vault/entries/${id}`, {
      method: 'PUT',
      body: JSON.stringify(entryData),
    });
  }

  async deleteVaultEntry(id) {
    return await this.request(`/vault/entries/${id}`, {
      method: 'DELETE',
    });
  }

  async getVaultAuditLog(id) {
    return await this.request(`/vault/entries/${id}/audit`);
  }

  // Wiki
  async searchWiki(query) {
    return await this.request(`/wiki/search?q=${encodeURIComponent(query)}`);
  }

  async getWikiPages(spaceId, parentPageId) {
    const parentQuery = parentPageId !== undefined && parentPageId !== null ? `&parentPageId=${parentPageId}` : '';
    return await this.request(`/wiki/pages?spaceId=${spaceId}${parentQuery}`);
  }

  async getWikiPage(id) {
    return await this.request(`/wiki/pages/${id}`);
  }

  async createWikiPage(pageData) {
    return await this.request('/wiki/pages', {
      method: 'POST',
      body: JSON.stringify(pageData),
    });
  }

  async updateWikiPage(id, pageData) {
    return await this.request(`/wiki/pages/${id}`, {
      method: 'PUT',
      body: JSON.stringify(pageData),
    });
  }

  async deleteWikiPage(id) {
    return await this.request(`/wiki/pages/${id}`, {
      method: 'DELETE',
    });
  }

  async moveWikiPage(id, newParentPageId) {
    return await this.request(`/wiki/pages/${id}/move`, {
      method: 'PUT',
      body: JSON.stringify({ newParentPageId }),
    });
  }

  async reorderWikiPages(spaceId, parentPageId, orderedPageIds) {
    const parentQuery = parentPageId !== undefined && parentPageId !== null ? `&parentPageId=${parentPageId}` : '';
    return await this.request(`/wiki/pages/reorder?spaceId=${spaceId}${parentQuery}`, {
      method: 'PUT',
      body: JSON.stringify({ orderedPageIds }),
    });
  }

  async setWikiPageLabels(id, labels) {
    return await this.request(`/wiki/pages/${id}/labels`, {
      method: 'PUT',
      body: JSON.stringify({ labels }),
    });
  }

  async getWikiVersions(id) {
    return await this.request(`/wiki/pages/${id}/versions`);
  }

  async getWikiVersion(id, versionNumber) {
    return await this.request(`/wiki/pages/${id}/versions/${versionNumber}`);
  }

  async getWikiComments(id) {
    return await this.request(`/wiki/pages/${id}/comments`);
  }

  async addWikiComment(id, commentData) {
    return await this.request(`/wiki/pages/${id}/comments`, {
      method: 'POST',
      body: JSON.stringify(commentData),
    });
  }

  async deleteWikiComment(commentId) {
    return await this.request(`/wiki/comments/${commentId}`, {
      method: 'DELETE',
    });
  }

  // Library (SharePoint-style file libraries - third Space-scoped consumer alongside Vault/Wiki).
  // Uploads/downloads bypass request() since it always assumes a JSON body/response - multipart
  // needs the browser to set its own Content-Type boundary, and downloads need the raw blob.
  async getLibraryFiles(spaceId) {
    return await this.request(`/library/files?spaceId=${spaceId}`);
  }

  async getLibraryFile(id) {
    return await this.request(`/library/files/${id}`);
  }

  async uploadLibraryFile(spaceId, file) {
    const form = new FormData();
    form.append('spaceId', spaceId);
    form.append('file', file);
    return await this.requestMultipart('/library/files', form);
  }

  async uploadLibraryFileVersion(id, file, comment) {
    const form = new FormData();
    form.append('file', file);
    if (comment) form.append('comment', comment);
    return await this.requestMultipart(`/library/files/${id}/versions`, form, 'POST', true);
  }

  async getLibraryFileVersions(id) {
    return await this.request(`/library/files/${id}/versions`);
  }

  async downloadLibraryFile(id, fileName) {
    return await this.downloadBlob(`/library/files/${id}/download`, fileName);
  }

  async downloadLibraryFileVersion(id, versionNumber, fileName) {
    return await this.downloadBlob(`/library/files/${id}/versions/${versionNumber}/download`, fileName);
  }

  async deleteLibraryFile(id) {
    return await this.request(`/library/files/${id}`, {
      method: 'DELETE',
    });
  }

  async requestMultipart(endpoint, form, method = 'POST', noContent = false) {
    const url = `${API_BASE_URL}${endpoint}`;
    // File uploads get the longer UPLOAD_TIMEOUT_MS bound (not the default) and are never
    // auto-retried - re-sending a large file transparently on a flaky connection would be wasteful
    // and, for a non-idempotent upload, could create a duplicate.
    const response = await this.fetchWithTimeout(url, {
      method,
      headers: {
        ...(this.token && { Authorization: `Bearer ${this.token}` }),
      },
      body: form,
    }, UPLOAD_TIMEOUT_MS);

    if (response.status === 401) {
      this.token = null;
      localStorage.removeItem('jwt_token');
      window.location.reload();
      return null;
    }

    if (!response.ok) {
      throw new Error(`API Error: ${response.status} ${response.statusText}`);
    }

    if (noContent || response.status === 204) return null;
    return await response.json();
  }

  async downloadBlob(endpoint, fileName) {
    const url = `${API_BASE_URL}${endpoint}`;
    // Same longer bound as requestMultipart - downloading a file over a slow connection legitimately
    // takes longer than a normal JSON request.
    const response = await this.fetchWithTimeout(url, {
      headers: {
        ...(this.token && { Authorization: `Bearer ${this.token}` }),
      },
    }, UPLOAD_TIMEOUT_MS);

    if (!response.ok) {
      throw new Error(`API Error: ${response.status} ${response.statusText}`);
    }

    const blob = await response.blob();
    const blobUrl = window.URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = blobUrl;
    link.download = fileName || 'download';
    document.body.appendChild(link);
    link.click();
    link.remove();
    window.URL.revokeObjectURL(blobUrl);
  }

  // Ideas board (company-wide, flat - see plan Phase 11)
  async getIdeas(status) {
    return await this.request(`/ideas${status ? `?status=${status}` : ''}`);
  }

  async getIdea(id) {
    return await this.request(`/ideas/${id}`);
  }

  async createIdea(dto) {
    return await this.request('/ideas', {
      method: 'POST',
      body: JSON.stringify(dto),
    });
  }

  async updateIdea(id, dto) {
    return await this.request(`/ideas/${id}`, {
      method: 'PUT',
      body: JSON.stringify(dto),
    });
  }

  async updateIdeaStatus(id, status) {
    return await this.request(`/ideas/${id}/status`, {
      method: 'PUT',
      body: JSON.stringify({ status }),
    });
  }

  async convertIdeaToProject(id) {
    return await this.request(`/ideas/${id}/convert-to-project`, {
      method: 'POST',
    });
  }

  async getIdeaComments(id) {
    return await this.request(`/ideas/${id}/comments`);
  }

  async addIdeaComment(id, dto) {
    return await this.request(`/ideas/${id}/comments`, {
      method: 'POST',
      body: JSON.stringify(dto),
    });
  }

  async deleteIdeaComment(commentId) {
    return await this.request(`/ideas/comments/${commentId}`, {
      method: 'DELETE',
    });
  }

  async getIdeaAttachments(id) {
    return await this.request(`/ideas/${id}/attachments`);
  }

  async uploadIdeaAttachment(id, file) {
    const form = new FormData();
    form.append('file', file);
    return await this.requestMultipart(`/ideas/${id}/attachments`, form);
  }

  async downloadIdeaAttachment(attachmentId, fileName) {
    return await this.downloadBlob(`/ideas/attachments/${attachmentId}/download`, fileName);
  }

  async deleteIdeaAttachment(attachmentId) {
    return await this.request(`/ideas/attachments/${attachmentId}`, {
      method: 'DELETE',
    });
  }

  async getIdeaAttachmentAnnotations(attachmentId) {
    return await this.request(`/ideas/attachments/${attachmentId}/annotations`);
  }

  async addIdeaAttachmentAnnotation(attachmentId, dto) {
    return await this.request(`/ideas/attachments/${attachmentId}/annotations`, {
      method: 'POST',
      body: JSON.stringify(dto),
    });
  }

  async deleteIdeaAttachmentAnnotation(annotationId) {
    return await this.request(`/ideas/annotations/${annotationId}`, {
      method: 'DELETE',
    });
  }

  // --- Reminders ---

  async getReminders(filters = {}) {
    const params = new URLSearchParams();
    Object.entries(filters).forEach(([key, value]) => {
      if (value !== undefined && value !== null && value !== '') params.set(key, value);
    });
    const qs = params.toString();
    return await this.request(`/reminders${qs ? `?${qs}` : ''}`);
  }

  async getReminderSummary() {
    return await this.request('/reminders/summary');
  }

  async getReminder(id) {
    return await this.request(`/reminders/${id}`);
  }

  async createReminder(dto) {
    return await this.request('/reminders', {
      method: 'POST',
      body: JSON.stringify(dto),
    });
  }

  async updateReminder(id, dto) {
    return await this.request(`/reminders/${id}`, {
      method: 'PUT',
      body: JSON.stringify(dto),
    });
  }

  async deleteReminder(id) {
    return await this.request(`/reminders/${id}`, {
      method: 'DELETE',
    });
  }

  async completeReminder(id) {
    return await this.request(`/reminders/${id}/complete`, {
      method: 'POST',
    });
  }

  async reopenReminder(id) {
    return await this.request(`/reminders/${id}/reopen`, {
      method: 'POST',
    });
  }

  async snoozeReminder(id, snoozeUntil) {
    return await this.request(`/reminders/${id}/snooze`, {
      method: 'POST',
      body: JSON.stringify({ snoozeUntil }),
    });
  }

  async duplicateReminder(id) {
    return await this.request(`/reminders/${id}/duplicate`, {
      method: 'POST',
    });
  }

  async bulkCompleteReminders(ids) {
    return await this.request('/reminders/bulk/complete', {
      method: 'POST',
      body: JSON.stringify({ ids }),
    });
  }

  async bulkDeleteReminders(ids) {
    return await this.request('/reminders/bulk', {
      method: 'DELETE',
      body: JSON.stringify({ ids }),
    });
  }

  async bulkRescheduleReminders(ids, dueAt) {
    return await this.request('/reminders/bulk/reschedule', {
      method: 'PUT',
      body: JSON.stringify({ ids, dueAt }),
    });
  }

  async bulkPriorityReminders(ids, priority) {
    return await this.request('/reminders/bulk/priority', {
      method: 'PUT',
      body: JSON.stringify({ ids, priority }),
    });
  }

  async bulkAssignReminders(ids, assignedToId) {
    return await this.request('/reminders/bulk/assign', {
      method: 'PUT',
      body: JSON.stringify({ ids, assignedToId }),
    });
  }

  // Timesheets (flat, ownership-based - see plan Phase 10). Only the dashboard widget consumes these
  // today; there's no dedicated Timesheets tab/UI yet.
  async getTimesheets(userId, status) {
    const params = new URLSearchParams();
    if (userId) params.append('userId', userId);
    if (status) params.append('status', status);
    const query = params.toString();
    return await this.request(`/timesheets${query ? `?${query}` : ''}`);
  }

  // Invoices (flat, finance.view/finance.manage - see plan Phase 8)
  async getInvoices() {
    return await this.request('/invoices');
  }

  async getInvoice(id) {
    return await this.request(`/invoices/${id}`);
  }

  async createInvoice(dto) {
    return await this.request('/invoices', {
      method: 'POST',
      body: JSON.stringify(dto),
    });
  }

  async deleteInvoice(id) {
    return await this.request(`/invoices/${id}`, {
      method: 'DELETE',
    });
  }

  async updateInvoiceStatus(id, status) {
    return await this.request(`/invoices/${id}/status`, {
      method: 'PUT',
      body: JSON.stringify({ status }),
    });
  }

  async uploadInvoiceFile(id, file) {
    const form = new FormData();
    form.append('file', file);
    return await this.requestMultipart(`/invoices/${id}/upload`, form);
  }

  async downloadInvoiceFile(id, fileName) {
    return await this.downloadBlob(`/invoices/${id}/download`, fileName);
  }

  async getInvoiceTemplates() {
    return await this.request('/invoices/templates');
  }

  async saveInvoiceAsTemplate(invoiceId, dto) {
    return await this.request(`/invoices/${invoiceId}/save-as-template`, {
      method: 'POST',
      body: JSON.stringify(dto),
    });
  }

  async createInvoiceFromTemplate(templateId, dto) {
    return await this.request(`/invoices/from-template/${templateId}`, {
      method: 'POST',
      body: JSON.stringify(dto),
    });
  }

  async deleteInvoiceTemplate(id) {
    return await this.request(`/invoices/templates/${id}`, {
      method: 'DELETE',
    });
  }

  async logout() {
    const refreshToken = this.refreshToken;
    this.token = null;
    this.refreshToken = null;
    localStorage.removeItem('jwt_token');
    localStorage.removeItem('refresh_token');

    if (refreshToken) {
      // Best-effort server-side revocation - the client-side tokens are already cleared either way,
      // so this is bounded rather than left to hang indefinitely on a dead connection.
      try {
        await this.fetchWithTimeout(`${API_BASE_URL}/auth/logout`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ token: refreshToken }),
        }, DEFAULT_TIMEOUT_MS);
      } catch (error) {
        console.error('Logout revocation failed:', error);
      }
    }
  }
}

export default ApiService;