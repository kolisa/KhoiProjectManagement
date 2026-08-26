// src/utils/apiError.js
// Shared catch-block helper: turns a failed API call into one toast, with one exception - a
// SessionExpiredError is deliberately NOT toasted here. AuthContext's global session-expired
// subscriber already shows a single "please log in again" toast and returns the user to the login
// screen; toasting it again per-component would just duplicate that message.
export const reportApiError = (toast, error, fallback = 'Something went wrong. Please try again.') => {
  if (error?.isSessionExpired) return;
  toast.error(error?.message || fallback);
};
