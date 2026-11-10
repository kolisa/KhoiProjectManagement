import React from 'react';

// Small shared pieces used by the backlog, the board and the item modal.

export const LabelChips = ({ labels, max = 3 }) => {
  if (!labels || labels.length === 0) return null;
  const shown = labels.slice(0, max);
  const extra = labels.length - shown.length;
  return (
    <span className="inline-flex flex-wrap items-center gap-1">
      {shown.map((l) => (
        <span key={l.id} className="inline-flex items-center gap-1 text-[10.5px] font-medium rounded-full px-1.5 py-0.5 bg-gray-100 text-gray-700">
          <span className="h-1.5 w-1.5 rounded-full" style={{ background: l.color }} aria-hidden="true" />
          {l.name}
        </span>
      ))}
      {extra > 0 && <span className="text-[10.5px] text-gray-500" title={labels.slice(max).map((l) => l.name).join(', ')}>+{extra}</span>}
    </span>
  );
};

// "5 of 8 done" progress for an Epic/Feature/Story, measured over the work underneath it.
export const RollupBar = ({ done, total, className = '' }) => {
  if (!total) return null;
  const pct = Math.round((done / total) * 100);
  return (
    <span className={`inline-flex items-center gap-1.5 ${className}`} title={`${done} of ${total} items done`}>
      <span className="h-1.5 w-16 rounded-full bg-gray-200 overflow-hidden" role="progressbar" aria-label="Progress of the work underneath" aria-valuenow={pct} aria-valuemin={0} aria-valuemax={100}>
        <span className="block h-full bg-green-500" style={{ width: `${pct}%` }} />
      </span>
      <span className="text-[11px] text-gray-600 tabular-nums">{done}/{total}</span>
    </span>
  );
};
