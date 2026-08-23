import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, it, expect, vi } from 'vitest';
import { server, API_BASE_URL } from '../../test/mswServer';
import ResetPasswordForm from './ResetPasswordForm';

describe('ResetPasswordForm', () => {
  it('rejects a password under 8 characters without calling the API', async () => {
    let called = false;
    server.use(
      http.post(`${API_BASE_URL}/auth/reset-password`, () => {
        called = true;
        return new HttpResponse(null, { status: 204 });
      })
    );
    const user = userEvent.setup();
    render(<ResetPasswordForm token="tok-123" onBackToLogin={vi.fn()} />);

    await user.type(screen.getByPlaceholderText(/^new password$/i), 'short1');
    await user.type(screen.getByPlaceholderText(/confirm new password/i), 'short1');
    await user.click(screen.getByRole('button', { name: /reset password/i }));

    expect(await screen.findByText(/at least 8 characters/i)).toBeInTheDocument();
    expect(called).toBe(false);
  });

  it('rejects mismatched passwords without calling the API', async () => {
    let called = false;
    server.use(
      http.post(`${API_BASE_URL}/auth/reset-password`, () => {
        called = true;
        return new HttpResponse(null, { status: 204 });
      })
    );
    const user = userEvent.setup();
    render(<ResetPasswordForm token="tok-123" onBackToLogin={vi.fn()} />);

    await user.type(screen.getByPlaceholderText(/^new password$/i), 'LongEnough1!');
    await user.type(screen.getByPlaceholderText(/confirm new password/i), 'Different1!');
    await user.click(screen.getByRole('button', { name: /reset password/i }));

    expect(await screen.findByText(/do not match/i)).toBeInTheDocument();
    expect(called).toBe(false);
  });

  it('shows a success state after a valid reset', async () => {
    server.use(
      http.post(`${API_BASE_URL}/auth/reset-password`, () => new HttpResponse(null, { status: 204 }))
    );
    const user = userEvent.setup();
    render(<ResetPasswordForm token="tok-123" onBackToLogin={vi.fn()} />);

    await user.type(screen.getByPlaceholderText(/^new password$/i), 'LongEnough1!');
    await user.type(screen.getByPlaceholderText(/confirm new password/i), 'LongEnough1!');
    await user.click(screen.getByRole('button', { name: /reset password/i }));

    await waitFor(() => expect(screen.getByText(/password reset$/i)).toBeInTheDocument());
  });

  it('shows an invalid-link error when the server rejects the token, and the form stays usable', async () => {
    server.use(
      http.post(`${API_BASE_URL}/auth/reset-password`, () => HttpResponse.json({ message: 'Invalid or expired reset link' }, { status: 400 }))
    );
    const user = userEvent.setup();
    render(<ResetPasswordForm token="expired-token" onBackToLogin={vi.fn()} />);

    await user.type(screen.getByPlaceholderText(/^new password$/i), 'LongEnough1!');
    await user.type(screen.getByPlaceholderText(/confirm new password/i), 'LongEnough1!');
    await user.click(screen.getByRole('button', { name: /reset password/i }));

    expect(await screen.findByText(/invalid or has expired/i)).toBeInTheDocument();
    expect(screen.queryByText(/password reset$/i)).not.toBeInTheDocument();
  });
});
