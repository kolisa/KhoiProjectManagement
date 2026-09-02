// src/components/Timesheets/duration.js
// Duration is entered as separate Hours/Minutes fields (minutes matter - "1.5" reads worse and is
// easy to get wrong for anything that isn't a quarter-hour) but stored/sent as the single decimal
// `hours` TimesheetEntryDto.Hours actually expects - these just convert at the edges. Split out from
// TimesheetDetail.jsx so timesheetExport.js can use formatDuration too without a circular import.
export const splitHours = (decimalHours) => {
  const total = Number(decimalHours) || 0;
  const wholeHours = Math.floor(total);
  const minutes = Math.round((total - wholeHours) * 60);
  return { wholeHours, minutes };
};

export const combineHours = (wholeHours, minutes) => Number(wholeHours || 0) + Number(minutes || 0) / 60;

export const formatDuration = (decimalHours) => {
  const { wholeHours, minutes } = splitHours(decimalHours);
  if (wholeHours === 0) return `${minutes}m`;
  if (minutes === 0) return `${wholeHours}h`;
  return `${wholeHours}h ${minutes}m`;
};
