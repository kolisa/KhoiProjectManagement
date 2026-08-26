import { delay, http, HttpResponse } from 'msw';
import { describe, expect, it, vi, beforeEach, afterEach } from 'vitest';
import { server, API_BASE_URL } from '../test/mswServer';
import ApiService, { NetworkError, SessionExpiredError, onSessionExpired } from './ApiService';

describe('ApiService.request - timeout and retry', () => {
  it('throws a NetworkError (not a hang) when a request exceeds its timeout', async () => {
    server.use(
      http.get(`${API_BASE_URL}/slow-endpoint`, async () => {
        await delay(200);
        return HttpResponse.json({ ok: true });
      })
    );
    const apiService = new ApiService();

    await expect(
      apiService.request('/slow-endpoint', {}, { timeoutMs: 20 })
    ).rejects.toThrow(NetworkError);
  });

  it('transparently retries a GET once after a network-level failure, and succeeds if the retry works', async () => {
    let callCount = 0;
    server.use(
      http.get(`${API_BASE_URL}/flaky-endpoint`, () => {
        callCount += 1;
        if (callCount === 1) {
          return HttpResponse.error(); // simulates a real network failure, not an HTTP error response
        }
        return HttpResponse.json({ ok: true, attempt: callCount });
      })
    );
    const apiService = new ApiService();

    const result = await apiService.request('/flaky-endpoint');

    expect(callCount).toBe(2);
    expect(result).toEqual({ ok: true, attempt: 2 });
  });

  it('gives up and throws NetworkError if a GET still fails after the one retry', async () => {
    let callCount = 0;
    server.use(
      http.get(`${API_BASE_URL}/always-down`, () => {
        callCount += 1;
        return HttpResponse.error();
      })
    );
    const apiService = new ApiService();

    await expect(apiService.request('/always-down')).rejects.toThrow(NetworkError);
    expect(callCount).toBe(2); // the original attempt plus exactly one retry, not an unbounded loop
  });

  it('does not retry a non-GET request after a network-level failure', async () => {
    let callCount = 0;
    server.use(
      http.post(`${API_BASE_URL}/create-something`, () => {
        callCount += 1;
        return HttpResponse.error();
      })
    );
    const apiService = new ApiService();

    await expect(
      apiService.request('/create-something', { method: 'POST', body: '{}' })
    ).rejects.toThrow(NetworkError);
    expect(callCount).toBe(1); // never retried - a timed-out POST may already have been applied server-side
  });

  it('does not retry a real HTTP error response (only network-level failures)', async () => {
    let callCount = 0;
    server.use(
      http.get(`${API_BASE_URL}/server-error`, () => {
        callCount += 1;
        return HttpResponse.json({ message: 'boom' }, { status: 500 });
      })
    );
    const apiService = new ApiService();

    await expect(apiService.request('/server-error')).rejects.toThrow('boom');
    expect(callCount).toBe(1);
  });
});

describe('ApiService.request - error message extraction', () => {
  it('surfaces the backend { message } as the thrown error text', async () => {
    server.use(
      http.post(`${API_BASE_URL}/widgets`, () => HttpResponse.json({ message: 'Widget name already taken' }, { status: 400 }))
    );
    const apiService = new ApiService();

    await expect(apiService.request('/widgets', { method: 'POST', body: '{}' })).rejects.toThrow('Widget name already taken');
  });

  it('surfaces the first FluentValidation field message from { errors }', async () => {
    server.use(
      http.post(`${API_BASE_URL}/widgets`, () =>
        HttpResponse.json({ errors: { Name: ['Name is required'], Color: ['Color is required'] } }, { status: 400 })
      )
    );
    const apiService = new ApiService();

    await expect(apiService.request('/widgets', { method: 'POST', body: '{}' })).rejects.toThrow('Name is required');
  });

  it('falls back to a generic status message when the error body has neither shape', async () => {
    server.use(
      http.post(`${API_BASE_URL}/widgets`, () => new HttpResponse(null, { status: 502, statusText: 'Bad Gateway' }))
    );
    const apiService = new ApiService();

    await expect(apiService.request('/widgets', { method: 'POST', body: '{}' })).rejects.toThrow('API Error: 502');
  });
});

describe('ApiService.request - silent token refresh on 401', () => {
  beforeEach(() => {
    localStorage.setItem('jwt_token', 'expired-token');
    localStorage.setItem('refresh_token', 'valid-refresh-token');
  });

  afterEach(() => {
    localStorage.clear();
  });

  it('refreshes the access token once and retries the original request on a 401', async () => {
    let projectsCallCount = 0;
    let refreshCallCount = 0;
    server.use(
      http.get(`${API_BASE_URL}/projects`, ({ request }) => {
        projectsCallCount += 1;
        const authHeader = request.headers.get('Authorization');
        if (authHeader === 'Bearer expired-token') {
          return new HttpResponse(null, { status: 401 });
        }
        return HttpResponse.json([{ id: 1, name: 'Refreshed project' }]);
      }),
      http.post(`${API_BASE_URL}/auth/refresh`, async ({ request }) => {
        refreshCallCount += 1;
        const body = await request.json();
        expect(body.token).toBe('valid-refresh-token');
        return HttpResponse.json({ token: 'new-token', refreshToken: 'new-refresh-token' });
      })
    );
    const apiService = new ApiService();

    const result = await apiService.request('/projects');

    expect(result).toEqual([{ id: 1, name: 'Refreshed project' }]);
    expect(projectsCallCount).toBe(2); // the original 401'd attempt, plus one retry with the new token
    expect(refreshCallCount).toBe(1);
    expect(apiService.token).toBe('new-token');
    expect(localStorage.getItem('jwt_token')).toBe('new-token');
  });

  it('de-dupes concurrent 401s into a single refresh call', async () => {
    let refreshCallCount = 0;
    server.use(
      http.get(`${API_BASE_URL}/projects`, ({ request }) => {
        const authHeader = request.headers.get('Authorization');
        if (authHeader === 'Bearer expired-token') return new HttpResponse(null, { status: 401 });
        return HttpResponse.json({ ok: true });
      }),
      http.get(`${API_BASE_URL}/tasks`, ({ request }) => {
        const authHeader = request.headers.get('Authorization');
        if (authHeader === 'Bearer expired-token') return new HttpResponse(null, { status: 401 });
        return HttpResponse.json({ ok: true });
      }),
      http.post(`${API_BASE_URL}/auth/refresh`, async () => {
        refreshCallCount += 1;
        await delay(20);
        return HttpResponse.json({ token: 'new-token', refreshToken: 'new-refresh-token' });
      })
    );
    const apiService = new ApiService();

    const [projects, tasks] = await Promise.all([
      apiService.request('/projects'),
      apiService.request('/tasks'),
    ]);

    expect(projects).toEqual({ ok: true });
    expect(tasks).toEqual({ ok: true });
    expect(refreshCallCount).toBe(1);
  });

  it('throws SessionExpiredError and notifies subscribers when the refresh token itself is rejected', async () => {
    server.use(
      http.get(`${API_BASE_URL}/projects`, () => new HttpResponse(null, { status: 401 })),
      http.post(`${API_BASE_URL}/auth/refresh`, () => new HttpResponse(null, { status: 401 }))
    );
    const apiService = new ApiService();
    const handler = vi.fn();
    const unsubscribe = onSessionExpired(handler);

    await expect(apiService.request('/projects')).rejects.toThrow(SessionExpiredError);

    expect(handler).toHaveBeenCalledTimes(1);
    expect(localStorage.getItem('jwt_token')).toBeNull();
    expect(localStorage.getItem('refresh_token')).toBeNull();
    unsubscribe();
  });

  it('never attempts a refresh for /auth/login itself, and reports it as a plain credentials error', async () => {
    let refreshCallCount = 0;
    server.use(
      http.post(`${API_BASE_URL}/auth/login`, () => new HttpResponse(null, { status: 401 })),
      http.post(`${API_BASE_URL}/auth/refresh`, () => {
        refreshCallCount += 1;
        return HttpResponse.json({ token: 'new-token' });
      })
    );
    const apiService = new ApiService();

    await expect(apiService.request('/auth/login', { method: 'POST', body: '{}' })).rejects.toThrow('Invalid email or password');
    expect(refreshCallCount).toBe(0);
  });
});
