import React, { useState, useEffect, useCallback, useMemo } from 'react';
import LoadingSpinner from '../Common/LoadingSpinner';
import ErrorMessage from '../Common/ErrorMessage';
import PriorityBadge from '../Common/PriorityBadge';
import StatusBadge from '../Common/StatusBadge';
import { TYPE_STYLES, STATUS_STYLES, statusLabel } from './pmConstants';

const fmtDay = (iso) => new Date(iso).toLocaleDateString(undefined, { month: 'short', day: 'numeric', year: 'numeric' });

// Everything assigned to me, across every project I can see - overdue first - so nobody has to open each
// project to find out what is on their plate. Opening a row jumps to that project's backlog.
const PmMyWork = ({ apiService, onOpenProject }) => {
  const [items, setItems] = useState(null);
  const [error, setError] = useState(null);
  const [projectFilter, setProjectFilter] = useState('');

  const load = useCallback(async () => {
    try {
      setError(null);
      setItems(await apiService.getMyWork());
    } catch (err) {
      setError(err?.message || 'Could not load your work.');
    }
  }, [apiService]);

  useEffect(() => { load(); }, [load]);

  const projects = useMemo(() => {
    const map = new Map();
    (items || []).forEach((i) => map.set(i.projectId, i.projectName));
    return [...map.entries()].map(([id, name]) => ({ id, name })).sort((a, b) => a.name.localeCompare(b.name));
  }, [items]);

  if (error) return <ErrorMessage message={error} onRetry={load} />;
  if (!items) return <LoadingSpinner />;

  const shown = projectFilter ? items.filter((i) => String(i.projectId) === projectFilter) : items;
  const overdue = shown.filter((i) => i.isOverdue).length;
  const points = shown.reduce((sum, i) => sum + (i.storyPoints || 0), 0);

  if (items.length === 0) {
    return <div className="text-center py-16 text-gray-500 bg-white border border-dashed border-gray-300 rounded-xl">Nothing open is assigned to you. Nice.</div>;
  }

  return (
    <div>
      <div className="flex flex-wrap items-center gap-3 mb-3">
        <select aria-label="Filter by project" value={projectFilter} onChange={(e) => setProjectFilter(e.target.value)}
          className="border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-blue-500">
          <option value="">All projects</option>
          {projects.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}
        </select>
        <p className="text-xs text-gray-500" aria-live="polite">
          {shown.length} open item{shown.length === 1 ? '' : 's'} · {points} points
          {overdue > 0 && <span className="text-red-600 font-medium"> · {overdue} overdue</span>}
        </p>
      </div>

      <ul className="bg-white border border-gray-200 rounded-xl divide-y divide-gray-100 overflow-hidden">
        {shown.map((i) => (
          <li key={i.id}>
            <button onClick={() => onOpenProject?.(i.projectId)} className="w-full flex items-center gap-3 px-3 py-2.5 text-left hover:bg-gray-50"
              aria-label={`Open ${i.projectName} for ${i.title}`}>
              <span className={`px-2 py-0.5 rounded text-[11px] font-semibold ${TYPE_STYLES[i.type] || 'bg-gray-100 text-gray-700'}`}>{i.type}</span>
              <span className="flex-1 min-w-0">
                <span className="block text-sm font-medium text-gray-900 truncate">{i.title}</span>
                <span className="block text-xs text-gray-500 truncate">{i.projectName}{i.sprintName && ` · ${i.sprintName}`} · #{i.id}</span>
              </span>
              {i.storyPoints != null && <span className="text-xs font-semibold bg-gray-100 text-gray-700 rounded-full px-2 py-0.5" title="Story points">{i.storyPoints}</span>}
              <PriorityBadge priority={i.priority} />
              <StatusBadge status={i.status} label={statusLabel(i.status)} colorMap={STATUS_STYLES} />
              <span className={`text-xs w-24 text-right ${i.isOverdue ? 'text-red-600 font-medium' : 'text-gray-500'}`}>{i.isOverdue ? 'Overdue ' : ''}{fmtDay(i.dueDate)}</span>
            </button>
          </li>
        ))}
      </ul>
    </div>
  );
};

export default PmMyWork;
