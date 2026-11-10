import React from 'react';

// Small dependency-free SVG charts for sprint analytics. Each carries a text alternative (role="img"
// + aria-label) and a visually-hidden data table so the numbers aren't locked inside the picture.

const W = 560;
const H = 220;
const PAD = { top: 16, right: 16, bottom: 28, left: 36 };
const innerW = W - PAD.left - PAD.right;
const innerH = H - PAD.top - PAD.bottom;

const fmtDay = (iso) => new Date(iso).toLocaleDateString(undefined, { month: 'short', day: 'numeric' });

const Axes = ({ max, ticks = 4 }) => (
  <g className="text-gray-400" fontSize="10">
    {Array.from({ length: ticks + 1 }, (_, i) => {
      const v = Math.round((max / ticks) * i);
      const y = PAD.top + innerH - (v / (max || 1)) * innerH;
      return (
        <g key={i}>
          <line x1={PAD.left} x2={W - PAD.right} y1={y} y2={y} stroke="currentColor" strokeOpacity="0.2" />
          <text x={PAD.left - 6} y={y + 3} textAnchor="end" fill="currentColor">{v}</text>
        </g>
      );
    })}
  </g>
);

export const BurndownChart = ({ burndown }) => {
  const { points, committedPoints } = burndown;
  if (!points.length) return null;
  const max = Math.max(committedPoints, 1);
  const x = (i) => PAD.left + (points.length === 1 ? innerW / 2 : (i / (points.length - 1)) * innerW);
  const y = (v) => PAD.top + innerH - (v / max) * innerH;

  const ideal = points.map((p, i) => `${x(i)},${y(p.ideal)}`).join(' ');
  const actualPts = points.map((p, i) => (p.remaining == null ? null : `${x(i)},${y(p.remaining)}`)).filter(Boolean);
  const last = points.filter((p) => p.remaining != null).pop();

  return (
    <figure>
      <svg viewBox={`0 0 ${W} ${H}`} role="img" className="w-full h-auto"
        aria-label={`Burndown for ${burndown.name}: ${committedPoints} points committed${last ? `, ${last.remaining} remaining` : ''}.`}>
        <Axes max={max} />
        <polyline points={ideal} fill="none" stroke="#9ca3af" strokeWidth="2" strokeDasharray="5 4" />
        {actualPts.length > 0 && <polyline points={actualPts.join(' ')} fill="none" stroke="#2563eb" strokeWidth="2.5" />}
        {points.map((p, i) => p.remaining != null && <circle key={i} cx={x(i)} cy={y(p.remaining)} r="3" fill="#2563eb" />)}
        <g fontSize="10" className="text-gray-500" fill="currentColor">
          <text x={PAD.left} y={H - 8}>{fmtDay(points[0].date)}</text>
          <text x={W - PAD.right} y={H - 8} textAnchor="end">{fmtDay(points[points.length - 1].date)}</text>
        </g>
      </svg>
      <figcaption className="flex gap-4 text-xs text-gray-600 mt-1">
        <span className="inline-flex items-center gap-1.5"><span className="inline-block w-4 border-t-2 border-dashed border-gray-400" /> Ideal</span>
        <span className="inline-flex items-center gap-1.5"><span className="inline-block w-4 border-t-2 border-blue-600" /> Remaining points</span>
      </figcaption>
      <table className="sr-only">
        <caption>Burndown data for {burndown.name}</caption>
        <thead><tr><th>Date</th><th>Ideal</th><th>Remaining</th></tr></thead>
        <tbody>{points.map((p) => <tr key={p.date}><td>{fmtDay(p.date)}</td><td>{p.ideal}</td><td>{p.remaining ?? 'n/a'}</td></tr>)}</tbody>
      </table>
    </figure>
  );
};

export const VelocityChart = ({ velocity }) => {
  const { sprints, averageCompleted } = velocity;
  if (!sprints.length) return null;
  const max = Math.max(1, ...sprints.map((s) => Math.max(s.committedPoints, s.completedPoints)));
  const slot = innerW / sprints.length;
  const barW = Math.min(28, slot / 2.6);
  const y = (v) => PAD.top + innerH - (v / max) * innerH;

  return (
    <figure>
      <svg viewBox={`0 0 ${W} ${H}`} role="img" className="w-full h-auto"
        aria-label={`Velocity over the last ${sprints.length} sprints, averaging ${averageCompleted} points completed.`}>
        <Axes max={max} />
        {sprints.map((s, i) => {
          const cx = PAD.left + slot * i + slot / 2;
          return (
            <g key={s.sprintId}>
              <rect x={cx - barW - 1} y={y(s.committedPoints)} width={barW} height={PAD.top + innerH - y(s.committedPoints)} fill="#cbd5e1" rx="2" />
              <rect x={cx + 1} y={y(s.completedPoints)} width={barW} height={PAD.top + innerH - y(s.completedPoints)} fill="#2563eb" rx="2" />
              <text x={cx} y={H - 8} textAnchor="middle" fontSize="10" className="text-gray-500" fill="currentColor">
                {s.name.length > 10 ? `${s.name.slice(0, 9)}…` : s.name}
              </text>
            </g>
          );
        })}
      </svg>
      <figcaption className="flex flex-wrap gap-4 text-xs text-gray-600 mt-1">
        <span className="inline-flex items-center gap-1.5"><span className="inline-block w-3 h-3 rounded-sm bg-slate-300" /> Committed</span>
        <span className="inline-flex items-center gap-1.5"><span className="inline-block w-3 h-3 rounded-sm bg-blue-600" /> Completed</span>
        <span className="ml-auto font-medium text-gray-700">Average velocity: {averageCompleted} pts</span>
      </figcaption>
      <table className="sr-only">
        <caption>Velocity data</caption>
        <thead><tr><th>Sprint</th><th>Committed</th><th>Completed</th></tr></thead>
        <tbody>{sprints.map((s) => <tr key={s.sprintId}><td>{s.name}</td><td>{s.committedPoints}</td><td>{s.completedPoints}</td></tr>)}</tbody>
      </table>
    </figure>
  );
};
