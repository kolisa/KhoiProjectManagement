import { describe, it, expect } from 'vitest';
import { hasSpaceLevel } from './spaceLevel';

describe('hasSpaceLevel', () => {
  it('returns false when the caller has no effective level', () => {
    expect(hasSpaceLevel(null, 'Read')).toBe(false);
    expect(hasSpaceLevel(undefined, 'Read')).toBe(false);
  });

  it('Manage satisfies a Read requirement', () => {
    expect(hasSpaceLevel('Manage', 'Read')).toBe(true);
  });

  it('Read does not satisfy a Write requirement', () => {
    expect(hasSpaceLevel('Read', 'Write')).toBe(false);
  });

  it('an exact level match is sufficient', () => {
    expect(hasSpaceLevel('Write', 'Write')).toBe(true);
  });

  it('treats an unrecognized level as rank 0 (denies)', () => {
    expect(hasSpaceLevel('NotARealLevel', 'Read')).toBe(false);
  });
});
