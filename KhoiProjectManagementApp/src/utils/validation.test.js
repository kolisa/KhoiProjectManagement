import { describe, it, expect } from 'vitest';
import {
  validateProject,
  validateTask,
  validateVaultEntry,
  validateReminder,
  validateWikiPage,
  hasErrors,
} from './validation';

describe('validateProject', () => {
  const validProject = {
    name: 'Valid Project',
    description: 'A description',
    priority: 'medium',
    startDate: '2026-01-01',
    endDate: '2026-02-01',
  };

  it('returns no errors for a well-formed project', () => {
    expect(hasErrors(validateProject(validProject))).toBe(false);
  });

  it('requires a name', () => {
    const errors = validateProject({ ...validProject, name: '' });
    expect(errors.name).toBeDefined();
  });

  it('rejects a name over 200 characters', () => {
    const errors = validateProject({ ...validProject, name: 'a'.repeat(201) });
    expect(errors.name).toBeDefined();
  });

  it('accepts a name of exactly 200 characters', () => {
    const errors = validateProject({ ...validProject, name: 'a'.repeat(200) });
    expect(errors.name).toBeUndefined();
  });

  it('rejects a priority outside low/medium/high', () => {
    const errors = validateProject({ ...validProject, priority: 'urgent' });
    expect(errors.priority).toBeDefined();
  });

  it('rejects an end date before the start date', () => {
    const errors = validateProject({ ...validProject, startDate: '2026-03-01', endDate: '2026-02-01' });
    expect(errors.endDate).toBeDefined();
  });

  it('allows an end date equal to the start date', () => {
    const errors = validateProject({ ...validProject, startDate: '2026-01-01', endDate: '2026-01-01' });
    expect(errors.endDate).toBeUndefined();
  });
});

describe('validateTask', () => {
  it('requires a title', () => {
    const errors = validateTask({ title: '', description: '', priority: 'low' });
    expect(errors.title).toBeDefined();
  });

  it('accepts a well-formed task', () => {
    const errors = validateTask({ title: 'Do the thing', description: 'desc', priority: 'high' });
    expect(hasErrors(errors)).toBe(false);
  });
});

describe('validateVaultEntry', () => {
  it('requires a secret value on create', () => {
    const errors = validateVaultEntry({ name: 'Entry', secretValue: '', notes: '' }, { isCreate: true });
    expect(errors.secretValue).toBeDefined();
  });

  it('does not require a secret value on update (blank means unchanged)', () => {
    const errors = validateVaultEntry({ name: 'Entry', secretValue: '', notes: '' }, { isCreate: false });
    expect(errors.secretValue).toBeUndefined();
  });

  it('still requires a name on update', () => {
    const errors = validateVaultEntry({ name: '', secretValue: '', notes: '' }, { isCreate: false });
    expect(errors.name).toBeDefined();
  });
});

describe('validateWikiPage', () => {
  it('requires a title', () => {
    const errors = validateWikiPage({ title: '' });
    expect(errors.title).toBeDefined();
  });

  it('rejects a title over 300 characters', () => {
    const errors = validateWikiPage({ title: 'a'.repeat(301) });
    expect(errors.title).toBeDefined();
  });
});

describe('validateReminder', () => {
  const validReminder = {
    title: 'Follow up',
    description: 'desc',
    dueAt: '2026-01-01T09:00',
    priority: 'medium',
    category: 'work',
    channel: 'InApp',
  };

  it('accepts a well-formed reminder with no recurrence', () => {
    expect(hasErrors(validateReminder(validReminder))).toBe(false);
  });

  it('requires a due date', () => {
    const errors = validateReminder({ ...validReminder, dueAt: '' });
    expect(errors.dueAt).toBeDefined();
  });

  it('rejects an unknown channel', () => {
    const errors = validateReminder({ ...validReminder, channel: 'Carrier Pigeon' });
    expect(errors.channel).toBeDefined();
  });

  it('rejects an unknown recurrence type', () => {
    const errors = validateReminder({ ...validReminder, recurrenceType: 'Yearly' });
    expect(errors.recurrenceType).toBeDefined();
  });

  it('rejects a recurrence end date before the due date', () => {
    const errors = validateReminder({
      ...validReminder,
      recurrenceType: 'Weekly',
      recurrenceEndDate: '2025-12-31T09:00',
    });
    expect(errors.recurrenceEndDate).toBeDefined();
  });

  it('allows a recurrence end date on/after the due date', () => {
    const errors = validateReminder({
      ...validReminder,
      recurrenceType: 'Weekly',
      recurrenceEndDate: '2026-06-01T09:00',
    });
    expect(errors.recurrenceEndDate).toBeUndefined();
  });
});

describe('hasErrors', () => {
  it('is false for an empty errors object', () => {
    expect(hasErrors({})).toBe(false);
  });

  it('is true when any key is present', () => {
    expect(hasErrors({ name: 'required' })).toBe(true);
  });
});
