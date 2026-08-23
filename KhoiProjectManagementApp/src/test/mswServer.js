// src/test/mswServer.js - shared MSW (Mock Service Worker) server for component tests that call
// ApiService (i.e. real fetch()). Individual test files register per-test handlers with `server.use(...)`;
// see src/test/setup.js for the listen/resetHandlers/close lifecycle wired into every test run.
import { setupServer } from 'msw/node';

export const server = setupServer();

// Mirrors ApiService.js's own resolution exactly (`import.meta.env.VITE_API_URL || 'https://localhost:7148/api'`)
// rather than hardcoding one guess - this repo's .env.local overrides VITE_API_URL for local dev, and
// Vitest loads the same .env files Vite does, so hardcoding here would silently stop matching real requests.
export const API_BASE_URL = import.meta.env.VITE_API_URL || 'https://localhost:7148/api';
