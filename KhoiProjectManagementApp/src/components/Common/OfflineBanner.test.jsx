import { act, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it } from 'vitest';
import OfflineBanner from './OfflineBanner';

describe('OfflineBanner', () => {
  const setNavigatorOnLine = (value) => {
    Object.defineProperty(navigator, 'onLine', { configurable: true, value });
  };

  afterEach(() => {
    setNavigatorOnLine(true);
  });

  it('renders nothing while online', () => {
    setNavigatorOnLine(true);
    render(<OfflineBanner />);

    expect(screen.queryByText(/you're offline/i)).not.toBeInTheDocument();
  });

  it('shows the offline message when the browser is offline', () => {
    setNavigatorOnLine(false);
    render(<OfflineBanner />);

    expect(screen.getByText(/you're offline/i)).toBeInTheDocument();
  });

  it('disappears again once connectivity is restored', () => {
    setNavigatorOnLine(false);
    render(<OfflineBanner />);
    expect(screen.getByText(/you're offline/i)).toBeInTheDocument();

    act(() => {
      window.dispatchEvent(new Event('online'));
    });

    expect(screen.queryByText(/you're offline/i)).not.toBeInTheDocument();
  });
});
