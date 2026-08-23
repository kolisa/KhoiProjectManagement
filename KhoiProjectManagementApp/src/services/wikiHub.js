// src/services/wikiHub.js
// One SignalR connection per WikiPageDetail mount (created on mount, stopped on unmount) rather than a
// shared singleton - keeps join/leave lifecycle trivial: stopping the connection is what triggers the
// server's OnDisconnectedAsync cleanup (releases the edit lock, drops presence), so there's no separate
// "leave" call to remember on navigation away from a page.
import * as signalR from '@microsoft/signalr';

// Exported (only) for unit testing this URL transform in isolation - callers still go through
// createWikiHubConnection below.
export const getHubUrl = () => {
  const apiUrl = import.meta.env.VITE_API_URL || 'https://localhost:7148/api';
  return apiUrl.replace(/\/api\/?$/, '') + '/hubs/wiki';
};

export const createWikiHubConnection = (apiService) =>
  new signalR.HubConnectionBuilder()
    .withUrl(getHubUrl(), { accessTokenFactory: () => apiService.token })
    .withAutomaticReconnect()
    .build();

export const HubConnectionState = signalR.HubConnectionState;
