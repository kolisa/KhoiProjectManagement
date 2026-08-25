import { delay, http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import { server, API_BASE_URL } from '../test/mswServer';
import ApiService, { NetworkError } from './ApiService';

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

    await expect(apiService.request('/server-error')).rejects.toThrow('API Error: 500');
    expect(callCount).toBe(1);
  });
});
