import { describe, it, expect } from 'vitest';
import { formatFileSize } from './formatFileSize';

describe('formatFileSize', () => {
  it('formats zero bytes', () => {
    expect(formatFileSize(0)).toBe('0 B');
  });

  it('returns an empty string for null/undefined (unknown size)', () => {
    expect(formatFileSize(null)).toBe('');
    expect(formatFileSize(undefined)).toBe('');
  });

  it('formats bytes below 1024 as B', () => {
    expect(formatFileSize(512)).toBe('512 B');
  });

  it('formats the 1024-byte boundary as KB, not B', () => {
    expect(formatFileSize(1024)).toBe('1.0 KB');
  });

  it('formats kilobytes with one decimal place', () => {
    expect(formatFileSize(1536)).toBe('1.5 KB');
  });

  it('formats the 1MB boundary as MB, not KB', () => {
    expect(formatFileSize(1024 * 1024)).toBe('1.0 MB');
  });

  it('formats megabytes with one decimal place', () => {
    expect(formatFileSize(5 * 1024 * 1024)).toBe('5.0 MB');
  });
});
