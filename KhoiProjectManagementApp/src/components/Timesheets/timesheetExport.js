// src/components/Timesheets/timesheetExport.js
// CSV, not a real .xlsx binary - matches this app's own established export convention (see the
// backend's ReportExportService, which only ever generates Csv/Pdf, no Excel library anywhere in
// this codebase) and opens correctly in Excel/Sheets/Numbers with zero new dependencies either side.
import { formatDuration } from './duration';

// Same quoting rule as ReportExportService.EscapeCsvField on the backend, kept consistent even
// though this one runs client-side.
const escapeCsvField = (field) => {
  const value = String(field ?? '');
  return /[",\n]/.test(value) ? `"${value.replace(/"/g, '""')}"` : value;
};

const toCsvRow = (fields) => fields.map(escapeCsvField).join(',');

export const buildTimesheetCsv = (timesheet) => {
  const lines = [
    toCsvRow(['Timesheet', timesheet.userName]),
    toCsvRow(['Period', `${timesheet.periodStart.slice(0, 10)} to ${timesheet.periodEnd.slice(0, 10)}`]),
    toCsvRow(['Status', timesheet.status]),
    '',
    toCsvRow(['Date', 'Time', 'Project', 'Task', 'Duration (hrs)', 'Duration']),
  ];

  for (const entry of timesheet.entries) {
    const parsed = new Date(entry.entryDate);
    const hasTime = parsed.getUTCHours() !== 0 || parsed.getUTCMinutes() !== 0;
    const time = hasTime
      ? `${String(parsed.getUTCHours()).padStart(2, '0')}:${String(parsed.getUTCMinutes()).padStart(2, '0')}`
      : '';
    lines.push(toCsvRow([
      entry.entryDate.slice(0, 10),
      time,
      entry.projectId ? (entry.projectName || '') : 'Non-billable',
      entry.description || '',
      entry.hours,
      formatDuration(entry.hours),
    ]));
  }

  lines.push('', toCsvRow(['', '', '', 'Total', timesheet.totalHours, formatDuration(timesheet.totalHours)]));

  return lines.join('\r\n');
};

export const downloadTimesheetCsv = (timesheet) => {
  const csv = buildTimesheetCsv(timesheet);
  const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = `Timesheet_${timesheet.periodStart.slice(0, 10)}_to_${timesheet.periodEnd.slice(0, 10)}.csv`;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  URL.revokeObjectURL(url);
};
