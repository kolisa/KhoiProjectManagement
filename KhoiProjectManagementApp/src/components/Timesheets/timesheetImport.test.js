import { describe, expect, it } from 'vitest';
import { buildTimesheetCsv } from './timesheetExport';
import { parseTimesheetCsv } from './timesheetImport';

const projects = [{ id: 1, name: 'Alpha' }];

describe('parseTimesheetCsv', () => {
  it('round-trips buildTimesheetCsv\'s own output', () => {
    const timesheet = {
      userName: 'Jane Doe',
      periodStart: '2026-01-05T00:00:00Z',
      periodEnd: '2026-01-11T00:00:00Z',
      status: 'Draft',
      entries: [
        { entryDate: '2026-01-05T09:00:00Z', projectId: 1, projectName: 'Alpha', description: 'Standup Meeting', hours: 1 },
        // Non-billable (no projectId), and a description containing a comma - exercises the CSV
        // quoting round-trip, not just a naive split(',').
        { entryDate: '2026-01-06T00:00:00Z', projectId: null, projectName: null, description: 'Misc admin, filing', hours: 2.5 },
      ],
      totalHours: 3.5,
    };

    const csv = buildTimesheetCsv(timesheet);
    const result = parseTimesheetCsv(csv, projects);

    expect(result.periodStart).toBe('2026-01-05');
    expect(result.periodEnd).toBe('2026-01-11');
    expect(result.warnings).toEqual([]);
    expect(result.entries).toEqual([
      { entryDate: '2026-01-05', entryTime: '09:00', projectId: '1', projectName: 'Alpha', description: 'Standup Meeting', hours: '1' },
      { entryDate: '2026-01-06', entryTime: '', projectId: '', projectName: '', description: 'Misc admin, filing', hours: '2.5' },
    ]);
  });

  it('parses a minimal hand-built CSV with no Timesheet/Period/Status preamble', () => {
    const csv = 'Date,Project,Task,Duration (hrs)\n2026-02-01,,Some work,3\n';

    const result = parseTimesheetCsv(csv, projects);

    expect(result.periodStart).toBeNull();
    expect(result.periodEnd).toBeNull();
    expect(result.entries).toEqual([
      { entryDate: '2026-02-01', entryTime: '', projectId: '', projectName: '', description: 'Some work', hours: '3' },
    ]);
    expect(result.warnings).toEqual([]);
  });

  it('imports an unrecognized project as non-billable and warns', () => {
    const csv = 'Date,Project,Task,Duration (hrs)\n2026-02-01,Ghost Project,Work,4\n';

    const result = parseTimesheetCsv(csv, projects);

    expect(result.entries[0].projectId).toBe('');
    expect(result.warnings).toHaveLength(1);
    expect(result.warnings[0]).toMatch(/Ghost Project/);
  });

  it('skips a row with missing or invalid hours instead of failing the whole import', () => {
    const csv = 'Date,Project,Task,Duration (hrs)\n2026-02-01,,Good row,4\n2026-02-02,,Bad row,not-a-number\n';

    const result = parseTimesheetCsv(csv, projects);

    expect(result.entries).toHaveLength(1);
    expect(result.entries[0].description).toBe('Good row');
    expect(result.warnings).toHaveLength(1);
  });

  it('throws on a file with no recognizable header row', () => {
    expect(() => parseTimesheetCsv('not a timesheet at all, just some text')).toThrow();
    expect(() => parseTimesheetCsv('')).toThrow();
  });
});
