// src/contexts/AuthContext.test.jsx - covers the idle-timeout behavior added to AuthContext:
// IDLE_TIMEOUT_MINUTES (15) of no activity shows a warning modal WARNING_SECONDS (60) before an
// auto-logout, any activity resets the timer, and "Stay signed in" dismisses the warning without
// logging out. Renders AuthProvider directly (not the whole App) with a minimal probe component,
// rather than going through the full login-form UI, to keep fake-timer test setup manageable.
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { useEffect } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { server, API_BASE_URL } from '../test/mswServer';
import { AuthProvider, useAuth } from './AuthContext';
import { ToastProvider } from './ToastContext';

const loginResponse = {
  token: 'fake-jwt-token',
  refreshToken: 'fake-refresh-token',
  user: { id: 1, name: 'Test Admin', email: 'admin@khoitech.africa', role: 'admin', position: 'Owner', isActive: true },
  permissions: ['dashboard.view'],
  expiresAt: new Date(Date.now() + 900_000).toISOString(),
};

// Mirrors AuthContext.jsx's own IDLE_TIMEOUT_MINUTES/WARNING_SECONDS constants - kept in sync by
// hand since they're not exported (deliberately private to the module).
const WARNING_AT_MS = 14 * 60 * 1000; // idle timeout (15min) minus the warning window (60s)
const LOGOUT_AT_MS = 15 * 60 * 1000;

// Logs in automatically on mount so each test can jump straight to manipulating the idle clock,
// instead of driving the real LoginForm through userEvent under fake timers.
const Probe = () => {
  const { user, login } = useAuth();
  useEffect(() => {
    login('admin@khoitech.africa', 'admin123');
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);
  return <div data-testid="status">{user ? `logged-in:${user.name}` : 'logged-out'}</div>;
};

const renderAuth = () => render(
  <ToastProvider>
    <AuthProvider>
      <Probe />
    </AuthProvider>
  </ToastProvider>
);

// The idle timer that fires when `user` first becomes truthy is scheduled against the real clock
// (login resolves via MSW before fake timers are installed in these tests) - a synthetic activity
// event forces resetIdleTimer() to reschedule itself against the fake clock once it's active.
const primeFakeClock = () => {
  vi.useFakeTimers();
  window.dispatchEvent(new Event('mousedown'));
};

describe('AuthContext - idle timeout', () => {
  beforeEach(() => {
    localStorage.clear();
    server.use(
      http.post(`${API_BASE_URL}/auth/login`, () => HttpResponse.json(loginResponse)),
      http.post(`${API_BASE_URL}/auth/logout`, () => HttpResponse.json({}))
    );
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('shows a warning 60s before the 15-minute idle mark, then auto-logs-out at the mark', async () => {
    renderAuth();
    await screen.findByText('logged-in:Test Admin');
    primeFakeClock();

    // Just before the warning threshold - nothing shown yet.
    await vi.advanceTimersByTimeAsync(WARNING_AT_MS - 1000);
    expect(screen.queryByText(/still there/i)).not.toBeInTheDocument();

    // Past the warning threshold - countdown modal appears.
    await vi.advanceTimersByTimeAsync(2000);
    expect(screen.getByText(/still there/i)).toBeInTheDocument();

    // Past the full 15 minutes with no activity - auto-logout fires.
    await vi.advanceTimersByTimeAsync(LOGOUT_AT_MS - (WARNING_AT_MS + 2000) + 1000);
    await waitFor(() => expect(screen.getByTestId('status')).toHaveTextContent('logged-out'));
  });

  it('resets the timer on activity, so no warning appears at the original deadline', async () => {
    renderAuth();
    await screen.findByText('logged-in:Test Admin');
    primeFakeClock();

    await vi.advanceTimersByTimeAsync(10 * 60 * 1000);
    window.dispatchEvent(new Event('mousedown'));

    // Total elapsed since the last reset is now under 4 minutes, well short of the (reset) 14-minute
    // warning mark - if the reset hadn't taken effect, the original 14-minute mark (only 4 minutes
    // after this second reset) would already have fired the warning.
    await vi.advanceTimersByTimeAsync(3 * 60 * 1000 + 55 * 1000);
    expect(screen.queryByText(/still there/i)).not.toBeInTheDocument();
  });

  it('"Stay signed in" dismisses the warning without logging out', async () => {
    renderAuth();
    await screen.findByText('logged-in:Test Admin');
    primeFakeClock();
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });

    await vi.advanceTimersByTimeAsync(WARNING_AT_MS + 1000);
    expect(screen.getByText(/still there/i)).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: /stay signed in/i }));

    expect(screen.queryByText(/still there/i)).not.toBeInTheDocument();
    expect(screen.getByTestId('status')).toHaveTextContent('logged-in:Test Admin');
  });
});
