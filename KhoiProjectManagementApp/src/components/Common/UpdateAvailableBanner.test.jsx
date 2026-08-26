import { act, render, screen } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import UpdateAvailableBanner from './UpdateAvailableBanner';

describe('UpdateAvailableBanner', () => {
  const originalReload = window.location.reload;

  beforeEach(() => {
    Object.defineProperty(window, 'location', {
      configurable: true,
      value: { ...window.location, reload: vi.fn() },
    });
  });

  afterEach(() => {
    vi.restoreAllMocks();
    window.location.reload = originalReload;
  });

  it('renders nothing when version.json reports the build this tab already has', async () => {
    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({ buildId: 'test' }),
    });

    await act(async () => {
      render(<UpdateAvailableBanner />);
    });

    expect(screen.queryByText(/new version/i)).not.toBeInTheDocument();
  });

  it('shows the banner once version.json reports a different build id', async () => {
    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({ buildId: 'a-newer-build' }),
    });

    await act(async () => {
      render(<UpdateAvailableBanner />);
    });

    expect(screen.getByText(/new version/i)).toBeInTheDocument();
  });

  it('reloads the page when "Refresh to update" is clicked', async () => {
    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({ buildId: 'a-newer-build' }),
    });

    await act(async () => {
      render(<UpdateAvailableBanner />);
    });

    screen.getByText(/refresh to update/i).click();
    expect(window.location.reload).toHaveBeenCalled();
  });

  it('renders nothing when the version check fails (e.g. dev server with no version.json)', async () => {
    global.fetch = vi.fn().mockResolvedValue({ ok: false, status: 404 });

    await act(async () => {
      render(<UpdateAvailableBanner />);
    });

    expect(screen.queryByText(/new version/i)).not.toBeInTheDocument();
  });
});
