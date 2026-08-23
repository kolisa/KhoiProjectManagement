import { describe, it, expect } from 'vitest';
import { hasPermission } from './permissions';

describe('hasPermission', () => {
  it('returns true when the permission is present', () => {
    expect(hasPermission(['projects.create', 'projects.edit'], 'projects.create')).toBe(true);
  });

  it('returns false when the permission is absent', () => {
    expect(hasPermission(['projects.create'], 'projects.delete')).toBe(false);
  });

  it('returns false for an empty permission list', () => {
    expect(hasPermission([], 'projects.create')).toBe(false);
  });

  it('returns false rather than throwing when permissions is null/undefined', () => {
    expect(hasPermission(null, 'projects.create')).toBe(false);
    expect(hasPermission(undefined, 'projects.create')).toBe(false);
  });
});
