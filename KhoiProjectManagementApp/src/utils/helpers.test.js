import { describe, it, expect } from 'vitest';
import { getTeamMemberName, getProjectName, formatDate } from './helpers';

describe('getTeamMemberName', () => {
  const teamMembers = [{ id: 1, name: 'Alice' }, { id: 2, name: 'Bob' }];

  it('returns the matching member name', () => {
    expect(getTeamMemberName(2, teamMembers)).toBe('Bob');
  });

  it('returns "Unassigned" when no member matches', () => {
    expect(getTeamMemberName(999, teamMembers)).toBe('Unassigned');
  });
});

describe('getProjectName', () => {
  const projects = [{ id: 1, name: 'Apollo' }];

  it('returns the matching project name', () => {
    expect(getProjectName(1, projects)).toBe('Apollo');
  });

  it('returns "Unknown Project" when no project matches', () => {
    expect(getProjectName(999, projects)).toBe('Unknown Project');
  });
});

describe('formatDate', () => {
  it('formats an ISO date string using locale formatting', () => {
    // en-US test environment default - exact separators aren't asserted, just that it produced a
    // real formatted date, not "Invalid Date".
    expect(formatDate('2026-01-15')).not.toBe('Invalid Date');
    expect(formatDate('2026-01-15')).toMatch(/2026/);
  });
});
