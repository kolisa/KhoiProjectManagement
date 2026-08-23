import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, it, expect, vi } from 'vitest';
import { server, API_BASE_URL } from '../../test/mswServer';
import ForgotPasswordForm from './ForgotPasswordForm';

describe('ForgotPasswordForm', () => {
  it('shows the same success message whether or not the email is registered', async () => {
    server.use(
      http.post(`${API_BASE_URL}/auth/forgot-password`, () => new HttpResponse(null, { status: 204 }))
    );
    const user = userEvent.setup();
    render(<ForgotPasswordForm onBackToLogin={vi.fn()} />);

    await user.type(screen.getByPlaceholderText(/email address/i), 'someone@khoitech.africa');
    await user.click(screen.getByRole('button', { name: /send reset link/i }));

    await waitFor(() => expect(screen.getByText(/check your email/i)).toBeInTheDocument());
    expect(screen.getByText(/someone@khoitech.africa/i)).toBeInTheDocument();
  });

  it('shows a generic error and does not crash when the request fails', async () => {
    server.use(
      http.post(`${API_BASE_URL}/auth/forgot-password`, () => HttpResponse.json({ message: 'boom' }, { status: 500 }))
    );
    const user = userEvent.setup();
    render(<ForgotPasswordForm onBackToLogin={vi.fn()} />);

    await user.type(screen.getByPlaceholderText(/email address/i), 'someone@khoitech.africa');
    await user.click(screen.getByRole('button', { name: /send reset link/i }));

    await waitFor(() => expect(screen.getByText(/something went wrong/i)).toBeInTheDocument());
    // Must stay on the form, not silently show the "check your email" success state.
    expect(screen.queryByText(/check your email/i)).not.toBeInTheDocument();
  });

  it('calls onBackToLogin when the back button is clicked', async () => {
    const onBackToLogin = vi.fn();
    const user = userEvent.setup();
    render(<ForgotPasswordForm onBackToLogin={onBackToLogin} />);

    await user.click(screen.getByRole('button', { name: /back to sign in/i }));

    expect(onBackToLogin).toHaveBeenCalledTimes(1);
  });
});
