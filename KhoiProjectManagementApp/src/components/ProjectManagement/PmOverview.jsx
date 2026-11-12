import React, { useState, useEffect, useCallback } from 'react';
import LoadingSpinner from '../Common/LoadingSpinner';
import ErrorMessage from '../Common/ErrorMessage';
import PriorityBadge from '../Common/PriorityBadge';
import { STATUSES, STATUS_STYLES, TYPE_STYLES, statusLabel } from './pmConstants';

const SEGMENT_COLORS = { todo: 'bg-gray-300', 'in-progress': 'bg-blue-500', blocked: 'bg-red-500', completed: 'bg-green-500' };

const fmtDay = (iso) => new Date(iso).toLocaleDateString(undefined, { month: 'short', day: 'numeric' });
const fmtWhen = (iso) => new Date(iso).toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' });

const Tile = ({ label, value, tone = 'text-gray-900', hint }) => (
  <div className="bg-white border border-gray-200 rounded-xl px-4 py-3">
    <div className="text-xs text-gray-500">{label}</div>
    <div className={`text-2xl font-semibold tabular-nums ${tone}`}>{value}</div>
    {hint && <div className="text-[11px] text-gray-400">{hint}</div>}
  </div>
);

const Card = ({ title, children, empty }) => (
  <section className="bg-white border border-gray-200 rounded-xl p-4">
    <h3 className="text-sm font-semibold text-gray-900 mb-2">{title}</h3>
    {empty ? <p className="text-sm text-gray-500 py-2">{empty}</p> : children}
  </section>
);

const ItemRow = ({ item }) => (
  <li className="flex items-center gap-2 py-1.5 text-sm">
    <span className={`px-1.5 py-0.5 rounded-sm text-[10.5px] font-semibold ${TYPE_STYLES[item.type] || 'bg-gray-100 text-gray-700'}`}>{item.type}</span>
    <span className="flex-1 truncate text-gray-900" title={item.title}>#{item.id} {item.title}</span>
    <PriorityBadge priority={item.priority} />
    <span className={`text-xs w-16 text-right ${item.isOverdue ? 'text-red-600 font-medium' : 'text-gray-500'}`}>{item.hasDueDate ? fmtDay(item.dueDate) : '–'}</span>
  </li>
);

// The landing page for a project: where the sprint stands, what is at risk, what is mine, what just happened.
const PmOverview = ({ apiService, project }) => {
  const [data, setData] = useState(null);
  const [error, setError] = useState(null);

  const load = useCallback(async () => {
    try {
      setError(null);
      setData(await apiService.getPmOverview(project.id));
    } catch (err) {
      setError(err?.message || 'Could not load the overview.');
    }
  }, [apiService, project.id]);

  useEffect(() => { setData(null); load(); }, [load]);

  if (error) return <ErrorMessage message={error} onRetry={load} />;
  if (!data) return <LoadingSpinner />;

  const total = Object.values(data.statusCounts).reduce((a, b) => a + b, 0);
  const sprint = data.activeSprint;
  const sprintPct = sprint && sprint.totalPoints > 0 ? Math.round((sprint.donePoints / sprint.totalPoints) * 100) : 0;

  return (
    <div className="space-y-4">
      <div className="grid gap-3 grid-cols-2 lg:grid-cols-4">
        <Tile label="Open story points" value={data.openPoints} />
        <Tile label="Overdue" value={data.overdueCount} tone={data.overdueCount ? 'text-red-600' : 'text-gray-900'} />
        <Tile label="Blocked" value={data.blockedCount} tone={data.blockedCount ? 'text-red-600' : 'text-gray-900'} />
        <Tile label="Unassigned" value={data.unassignedCount} tone={data.unassignedCount ? 'text-amber-600' : 'text-gray-900'} hint="open items" />
      </div>

      <div className="grid gap-4 lg:grid-cols-2">
        <Card title="Active sprint" empty={sprint ? null : 'No sprint is running. Start one from the Sprints tab.'}>
          {sprint && (
            <div>
              <div className="flex items-baseline justify-between">
                <span className="font-medium text-gray-900">{sprint.name}</span>
                <span className="text-xs text-gray-500">{fmtDay(sprint.startDate)} – {fmtDay(sprint.endDate)} · {sprint.daysLeft} day{sprint.daysLeft === 1 ? '' : 's'} left</span>
              </div>
              {sprint.goal && <p className="text-sm text-gray-600 mt-1">{sprint.goal}</p>}
              <div className="mt-3 h-2.5 rounded-full bg-gray-100 overflow-hidden" role="progressbar" aria-label="Sprint progress" aria-valuenow={sprintPct} aria-valuemin={0} aria-valuemax={100}>
                <div className="h-full bg-green-500" style={{ width: `${sprintPct}%` }} />
              </div>
              <p className="text-xs text-gray-600 mt-1.5">{sprint.donePoints} of {sprint.totalPoints} points done · {sprint.itemsDone} of {sprint.itemsTotal} items</p>
            </div>
          )}
        </Card>

        <Card title="Work by status" empty={total === 0 ? 'No work items yet.' : null}>
          <div className="flex h-3 rounded-full overflow-hidden bg-gray-100" role="img"
            aria-label={STATUSES.map((s) => `${data.statusCounts[s.key] || 0} ${s.label}`).join(', ')}>
            {STATUSES.map((s) => (data.statusCounts[s.key] ? <div key={s.key} className={SEGMENT_COLORS[s.key]} style={{ width: `${(data.statusCounts[s.key] / total) * 100}%` }} /> : null))}
          </div>
          <ul className="mt-3 grid grid-cols-2 gap-x-4 gap-y-1 text-sm">
            {STATUSES.map((s) => (
              <li key={s.key} className="flex items-center gap-2">
                <span className={`h-2.5 w-2.5 rounded-xs ${SEGMENT_COLORS[s.key]}`} aria-hidden="true" />
                <span className="text-gray-700">{statusLabel(s.key)}</span>
                <span className="ml-auto tabular-nums text-gray-900">{data.statusCounts[s.key] || 0}</span>
              </li>
            ))}
          </ul>
        </Card>

        <Card title="Assigned to me" empty={data.myOpenItems.length === 0 ? 'Nothing open is assigned to you here.' : null}>
          <ul className="divide-y divide-gray-100">{data.myOpenItems.map((i) => <ItemRow key={i.id} item={i} />)}</ul>
        </Card>

        <Card title="Due in the next 7 days" empty={data.dueSoon.length === 0 ? 'Nothing is due this week.' : null}>
          <ul className="divide-y divide-gray-100">{data.dueSoon.map((i) => <ItemRow key={i.id} item={i} />)}</ul>
        </Card>
      </div>

      <Card title="Recent activity" empty={data.recentActivity.length === 0 ? 'No activity yet.' : null}>
        <ul className="divide-y divide-gray-100">
          {data.recentActivity.map((a, i) => (
            <li key={`${a.when}-${i}`} className="py-2 text-sm flex gap-2">
              <span className="text-gray-900"><span className="font-medium">{a.actorName}</span> {a.text}</span>
              <span className="ml-auto text-xs text-gray-500 whitespace-nowrap">{fmtWhen(a.when)}</span>
            </li>
          ))}
        </ul>
      </Card>
    </div>
  );
};

export default PmOverview;
