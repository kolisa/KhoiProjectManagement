// src/components/Timesheets/timesheetImport.js
// Client-side counterpart to timesheetExport.js - same "CSV, no library" convention (see that file's
// own comment). Parses a CSV back into the exact entry shape TimesheetDetail's `entries` state already
// uses, so an uploaded file drops straight into the existing editable grid/save/validate flow with no
// new rendering or persistence logic. Round-trips buildTimesheetCsv's own output, but is lenient about
// anything simpler (a hand-built file with just a header row and data rows, no Timesheet/Period/Status
// preamble) so it isn't limited to files this app itself produced.

// Minimal CSV tokenizer matching timesheetExport.js's own escapeCsvField quoting rule: a field
// containing `,`/`"`/newline is wrapped in quotes, with an internal `"` doubled to `""`. A naive
// split(',') would break on any quoted Task description that happens to contain a comma.
const parseCsvRows = (text) => {
  const rows = [];
  let row = [];
  let field = '';
  let inQuotes = false;

  const pushField = () => { row.push(field); field = ''; };
  const pushRow = () => { pushField(); rows.push(row); row = []; };

  for (let i = 0; i < text.length; i++) {
    const char = text[i];
    if (inQuotes) {
      if (char === '"') {
        if (text[i + 1] === '"') { field += '"'; i += 1; } else { inQuotes = false; }
      } else {
        field += char;
      }
      continue;
    }
    if (char === '"') { inQuotes = true; }
    else if (char === ',') { pushField(); }
    else if (char === '\r') { /* skip - \n handles the row break */ }
    else if (char === '\n') { pushRow(); }
    else { field += char; }
  }
  if (field.length > 0 || row.length > 0) pushRow();

  return rows;
};

const isBlankRow = (row) => row.length === 0 || (row.length === 1 && row[0] === '');

// Same "Date,Time,Project,Task,Duration (hrs),Duration" shape buildTimesheetCsv writes, located by
// column name rather than fixed position so a file re-saved through Excel/Sheets (columns possibly
// reordered) still parses. Date + an hours column are the minimum signal required to call something
// a header row.
const findHeaderRow = (rows) => {
  for (let i = 0; i < rows.length; i++) {
    const lower = rows[i].map((c) => c.trim().toLowerCase());
    const dateIndex = lower.indexOf('date');
    const hoursIndex = lower.findIndex((c) => c === 'hours' || c.startsWith('duration (hrs'));
    if (dateIndex !== -1 && hoursIndex !== -1) {
      return {
        index: i,
        columns: {
          date: dateIndex,
          time: lower.indexOf('time'),
          project: lower.indexOf('project'),
          task: lower.indexOf('task'),
          hours: hoursIndex,
        },
      };
    }
  }
  return null;
};

const NON_BILLABLE = 'non-billable';

/**
 * @param {string} text - raw CSV file content.
 * @param {Array<{id: number, name: string}>} projects - the caller's already-loaded project list,
 *   used to resolve the Project column's name back to a projectId.
 * @returns {{ periodStart: string|null, periodEnd: string|null, entries: object[], warnings: string[] }}
 * @throws {Error} if no recognizable header row (Date + an hours column) is found, or no data rows
 *   parse out of it - both mean "this doesn't look like a timesheet export" rather than a partial import.
 */
export const parseTimesheetCsv = (text, projects = []) => {
  const rows = parseCsvRows(text).map((r) => r.map((f) => f.trim()));

  let periodStart = null;
  let periodEnd = null;
  const periodRow = rows.find((r) => r[0]?.toLowerCase() === 'period');
  const periodMatch = periodRow?.[1]?.match(/(\d{4}-\d{2}-\d{2})\s*to\s*(\d{4}-\d{2}-\d{2})/i);
  if (periodMatch) {
    periodStart = periodMatch[1];
    periodEnd = periodMatch[2];
  }

  const header = findHeaderRow(rows);
  if (!header) {
    throw new Error('Could not find a recognizable Date/Hours header row in this file.');
  }
  const { date: dateCol, time: timeCol, project: projectCol, task: taskCol, hours: hoursCol } = header.columns;

  const projectByName = new Map(projects.map((p) => [p.name.trim().toLowerCase(), p]));
  const entries = [];
  const warnings = [];

  for (let i = header.index + 1; i < rows.length; i++) {
    const row = rows[i];
    if (isBlankRow(row)) break; // the exporter always puts a blank line before the Total row
    if (taskCol !== -1 && row[taskCol]?.toLowerCase() === 'total') break;

    const dateRaw = row[dateCol];
    const dateMatch = dateRaw?.match(/\d{4}-\d{2}-\d{2}/);
    if (!dateMatch) {
      warnings.push(`Row ${i + 1}: unrecognized date "${dateRaw || ''}" - skipped.`);
      continue;
    }

    const hoursRaw = hoursCol !== -1 ? row[hoursCol] : '';
    const hours = Number(hoursRaw);
    if (!hoursRaw || Number.isNaN(hours) || hours <= 0) {
      warnings.push(`Row ${i + 1} (${dateMatch[0]}): missing or invalid hours - skipped.`);
      continue;
    }

    const timeRaw = timeCol !== -1 ? row[timeCol] : '';
    const entryTime = /^\d{2}:\d{2}$/.test(timeRaw) ? timeRaw : '';

    const projectRaw = projectCol !== -1 ? row[projectCol] : '';
    let projectId = '';
    let projectName = '';
    if (projectRaw && projectRaw.toLowerCase() !== NON_BILLABLE) {
      const match = projectByName.get(projectRaw.toLowerCase());
      if (match) {
        projectId = String(match.id);
        projectName = match.name;
      } else {
        warnings.push(`Row ${i + 1}: unrecognized project "${projectRaw}" - imported as non-billable.`);
      }
    }

    entries.push({
      entryDate: dateMatch[0],
      entryTime,
      projectId,
      projectName,
      description: taskCol !== -1 ? (row[taskCol] || '') : '',
      hours: String(hours),
    });
  }

  if (entries.length === 0) {
    throw new Error('No timesheet rows were found in this file.');
  }

  return { periodStart, periodEnd, entries, warnings };
};
