import { afterEach, describe, expect, it, vi } from 'vitest';
import { createWikiHubConnection, getHubUrl, HubConnectionState } from './wikiHub';

describe('getHubUrl', () => {
  afterEach(() => {
    vi.unstubAllEnvs();
  });

  it('strips a trailing /api and appends /hubs/wiki', () => {
    vi.stubEnv('VITE_API_URL', 'https://localhost:7148/api');
    expect(getHubUrl()).toBe('https://localhost:7148/hubs/wiki');
  });

  it('strips a trailing /api/ (with slash) the same way', () => {
    vi.stubEnv('VITE_API_URL', 'https://localhost:7148/api/');
    expect(getHubUrl()).toBe('https://localhost:7148/hubs/wiki');
  });

  it('falls back to the default API URL when VITE_API_URL is unset', () => {
    vi.stubEnv('VITE_API_URL', '');
    expect(getHubUrl()).toBe('https://localhost:7148/hubs/wiki');
  });

  it('leaves a base URL with no /api suffix untouched before appending', () => {
    vi.stubEnv('VITE_API_URL', 'http://localhost:5278');
    expect(getHubUrl()).toBe('http://localhost:5278/hubs/wiki');
  });
});

describe('createWikiHubConnection', () => {
  it('builds a connection exposing the expected SignalR interface without connecting', () => {
    const apiService = { token: 'fake-jwt' };

    const connection = createWikiHubConnection(apiService);

    expect(typeof connection.start).toBe('function');
    expect(typeof connection.stop).toBe('function');
    expect(typeof connection.on).toBe('function');
    expect(typeof connection.invoke).toBe('function');
  });
});

describe('HubConnectionState', () => {
  it('re-exports the real SignalR enum, not a stand-in', () => {
    expect(HubConnectionState.Connected).toBeDefined();
    expect(HubConnectionState.Disconnected).toBeDefined();
  });
});
