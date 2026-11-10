import { describe, it, expect } from 'vitest';
import { can, allowedParentTypes, roleLabel } from './pmConstants';

describe('pmConstants', () => {
  it('mirrors the server role matrix', () => {
    expect(can('ProjectAdmin', 'members')).toBe(true);
    expect(can('ProjectManager', 'members')).toBe(false);
    expect(can('ScrumMaster', 'delete')).toBe(true);
    expect(can('Developer', 'delete')).toBe(false);
    expect(can('Developer', 'rank')).toBe(true);
    expect(can('Tester', 'rank')).toBe(false);
    expect(can('Viewer', 'create')).toBe(false);
    expect(can('SuperAdmin', 'members')).toBe(true);
    expect(can('Unknown', 'edit')).toBe(false);
  });

  it('only offers strictly higher-level parent types', () => {
    expect(allowedParentTypes('Epic')).toEqual([]);
    expect(allowedParentTypes('Feature')).toEqual(['Epic']);
    expect(allowedParentTypes('Story')).toEqual(['Epic', 'Feature']);
    expect(allowedParentTypes('Task')).toEqual(['Epic', 'Feature', 'Story']);
    expect(allowedParentTypes('Subtask')).toEqual(['Epic', 'Feature', 'Story', 'Task', 'Bug']);
    expect(allowedParentTypes('Meeting')).toEqual([]);
  });

  it('labels roles', () => {
    expect(roleLabel('ScrumMaster')).toBe('Scrum Master');
    expect(roleLabel('SuperAdmin')).toBe('Super Admin');
  });
});
