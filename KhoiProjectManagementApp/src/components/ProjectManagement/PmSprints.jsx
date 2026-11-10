import React, { useState, useEffect, useCallback, useMemo } from 'react';
import { Plus, Play, CheckCircle2, Pencil, Trash2, Target } from 'lucide-react';
import LoadingSpinner from '../Common/LoadingSpinner';
import ErrorMessage from '../Common/ErrorMessage';
import StatusBadge from '../Common/StatusBadge';
import { SprintModal, CompleteSprintModal } from './SprintModals';
import RetroModal from './RetroModal';
import SprintEffort from './SprintEffort';
import { BurndownChart, VelocityChart } from './PmCharts';
import { useToast } from '../../contexts/ToastContext';
import { useConfirm } from '../../contexts/ConfirmContext';
import { reportApiError } from '../../utils/apiError';
import { TYPE_STYLES, STATUS_STYLES, statusLabel, can, denyReason } from './pmConstants';

const CONTAINER_TYPES = ['Epic', 'Feature'];
const selectClass = 'border border-gray-300 rounded-lg px-2 py-1 text-xs bg-white text-gray-700';

const fmtRange = (s) => {
  const f = (d) => new Date(d).toLocaleDateString(undefined, { month: 'short', day: 'numeric' });
  return `${f(s.startDate)} – ${f(s.endDate)}`;
};

const ItemRow = ({ item, sprints, currentSprintId, canPlan, reason, onAssign }) => (
  <li className="flex items-center gap-3 px-3 py-2">
    <span className={`px-2 py-0.5 rounded text-[11px] font-semibold ${TYPE_STYLES[item.type] || 'bg-gray-100 text-gray-700'}`}>{item.type}</span>
    <span className="flex-1 min-w-0 text-sm text-gray-900 truncate">{item.title}</span>
    {item.storyPoints != null && <span className="text-xs font-semibold bg-gray-100 text-gray-700 rounded-full px-2 py-0.5" title="Story points">{item.storyPoints}</span>}
    <StatusBadge status={item.status} label={statusLabel(item.status)} colorMap={STATUS_STYLES} />
    <label>
      <span className="sr-only">Sprint for {item.title}</span>
      <select value={currentSprintId ?? ''} onChange={(e) => onAssign(item, e.target.value === '' ? null : Number(e.target.value))}
        disabled={!canPlan} title={reason || undefined} className={`${selectClass} disabled:bg-gray-50 disabled:text-gray-400 disabled:cursor-not-allowed`}>
        <option value="">Backlog</option>
        {sprints.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
      </select>
    </label>
  </li>
);

// Sprint planning + analytics. Items are the same work items the backlog and board use - planning just
// sets their SprintId. Only one sprint can be active; completing it locks its velocity.
const PmSprints = ({ apiService, project }) => {
  const toast = useToast();
  const confirm = useConfirm();
  const [sprints, setSprints] = useState([]);
  const [items, setItems] = useState([]);
  const [velocity, setVelocity] = useState(null);
  const [burndown, setBurndown] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [editing, setEditing] = useState(undefined); // undefined closed, null new, object edit
  const [completing, setCompleting] = useState(null);
  const [retroSprint, setRetroSprint] = useState(null);

  const canPlan = can(project.myRole, 'sprint');
  const planReason = denyReason(project, 'sprint');

  const load = useCallback(async () => {
    try {
      setError(null);
      const [s, i, v] = await Promise.all([
        apiService.getSprints(project.id),
        apiService.getWorkItems(project.id),
        apiService.getVelocity(project.id),
      ]);
      setSprints(s);
      setItems(i.filter((x) => !CONTAINER_TYPES.includes(x.type)));
      setVelocity(v);
      const active = s.find((x) => x.state === 'Active');
      setBurndown(active ? await apiService.getBurndown(active.id) : null);
    } catch (err) {
      setError(err?.message || 'Could not load sprints.');
    } finally {
      setLoading(false);
    }
  }, [apiService, project.id]);

  useEffect(() => { setLoading(true); load(); }, [load]);

  const act = async (fn, success) => {
    try {
      await fn();
      if (success) toast.success(success);
      await load();
    } catch (err) {
      reportApiError(toast, err);
    }
  };

  const open = useMemo(() => sprints.filter((s) => s.state !== 'Completed'), [sprints]);
  const planned = useMemo(() => sprints.filter((s) => s.state === 'Planned'), [sprints]);
  const completed = useMemo(() => sprints.filter((s) => s.state === 'Completed'), [sprints]);
  const hasActive = sprints.some((s) => s.state === 'Active');
  const backlogItems = items.filter((i) => i.sprintId == null && i.status !== 'completed');

  const assign = (item, sprintId) => act(() => apiService.assignWorkItemSprint(item.id, sprintId));

  const saveSprint = async (dto) => {
    if (editing) await apiService.updateSprint(editing.id, dto);
    else await apiService.createSprint(project.id, dto);
    toast.success(editing ? 'Sprint updated.' : 'Sprint created.');
    setEditing(undefined);
    await load();
  };

  const remove = async (sprint) => {
    if (!(await confirm(`Delete "${sprint.name}"? Its items go back to the backlog.`, { title: 'Delete sprint', confirmText: 'Delete', danger: true }))) return;
    act(() => apiService.deleteSprint(sprint.id), 'Sprint deleted.');
  };

  const complete = async (moveTo) => {
    await apiService.completeSprint(completing.id, moveTo);
    toast.success('Sprint completed.');
    setCompleting(null);
    await load();
  };

  if (loading) return <LoadingSpinner />;
  if (error) return <ErrorMessage message={error} onRetry={load} />;

  return (
    <div className="space-y-6">
      <div className="flex items-center">
        <p className="text-sm text-gray-500">Plan work into time-boxed sprints. Epics and Features are planned through their stories and tasks.</p>
        <button onClick={() => setEditing(null)} disabled={!canPlan} title={planReason || undefined}
          className="ml-auto inline-flex items-center gap-1.5 px-4 py-2 rounded-lg bg-blue-600 text-white text-sm font-medium hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed">
          <Plus className="h-4 w-4" aria-hidden="true" /> New sprint
        </button>
      </div>

      {open.map((sprint) => {
        const sprintItems = items.filter((i) => i.sprintId === sprint.id);
        const unfinished = sprintItems.filter((i) => i.status !== 'completed').length;
        const over = sprint.capacity != null && sprint.totalPoints > sprint.capacity;
        const pct = sprint.capacity ? Math.min(100, Math.round((sprint.totalPoints / sprint.capacity) * 100)) : 0;
        const otherSprints = open;
        return (
          <section key={sprint.id} aria-label={sprint.name} className="bg-white border border-gray-200 rounded-xl">
            <header className="flex flex-wrap items-start gap-3 p-4 border-b border-gray-100">
              <div className="flex-1 min-w-[200px]">
                <div className="flex items-center gap-2">
                  <h3 className="text-base font-semibold text-gray-900">{sprint.name}</h3>
                  <span className={`text-[11px] font-semibold rounded px-2 py-0.5 ${sprint.state === 'Active' ? 'bg-green-100 text-green-700' : 'bg-gray-100 text-gray-600'}`}>{sprint.state}</span>
                </div>
                <p className="text-xs text-gray-500 mt-0.5">{fmtRange(sprint)} · {sprint.itemCount} item{sprint.itemCount === 1 ? '' : 's'}</p>
                {sprint.goal && <p className="text-sm text-gray-700 mt-2 flex gap-1.5"><Target className="h-4 w-4 text-blue-500 flex-shrink-0 mt-0.5" aria-hidden="true" />{sprint.goal}</p>}
              </div>
              <div className="w-56">
                <div className="flex justify-between text-xs text-gray-600 mb-1">
                  <span>{sprint.totalPoints} pts planned</span>
                  <span>{sprint.capacity != null ? `capacity ${sprint.capacity}` : 'no capacity set'}</span>
                </div>
                {sprint.capacity != null && (
                  <div className="h-2 rounded-full bg-gray-100 overflow-hidden" role="progressbar" aria-label={`${sprint.name} load`} aria-valuenow={sprint.totalPoints} aria-valuemin={0} aria-valuemax={sprint.capacity}>
                    <div className={`h-full ${over ? 'bg-red-500' : 'bg-blue-500'}`} style={{ width: `${pct}%` }} />
                  </div>
                )}
                {over && <p className="text-xs text-red-600 mt-1">Over capacity by {sprint.totalPoints - sprint.capacity} pts</p>}
                {sprint.state === 'Active' && <p className="text-xs text-gray-500 mt-1">{sprint.donePoints} pts done</p>}
              </div>
              {sprint.state !== 'Planned' && (
                <button onClick={() => setRetroSprint(sprint)} className="px-3 py-1.5 rounded-lg border border-gray-300 text-sm text-gray-700 hover:bg-gray-50">Retro</button>
              )}
              <div className="flex items-center gap-2">
                {sprint.state === 'Planned' && (
                  <button onClick={() => act(() => apiService.startSprint(sprint.id), 'Sprint started.')} disabled={!canPlan || hasActive}
                    title={planReason || (hasActive ? 'Complete the active sprint first' : 'Start sprint')}
                    className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg border border-gray-300 text-sm text-gray-700 hover:bg-gray-50 disabled:opacity-50 disabled:cursor-not-allowed">
                    <Play className="h-4 w-4" aria-hidden="true" /> Start
                  </button>
                )}
                {sprint.state === 'Active' && (
                  <button onClick={() => setCompleting(sprint)} disabled={!canPlan} title={planReason || undefined}
                    className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-green-600 text-white text-sm font-medium hover:bg-green-700 disabled:opacity-50 disabled:cursor-not-allowed">
                    <CheckCircle2 className="h-4 w-4" aria-hidden="true" /> Complete
                  </button>
                )}
                <button aria-label={`Edit ${sprint.name}`} onClick={() => setEditing(sprint)} disabled={!canPlan} title={planReason || undefined}
                  className="text-gray-400 hover:text-blue-600 disabled:text-gray-300 disabled:hover:text-gray-300 disabled:cursor-not-allowed"><Pencil className="h-4 w-4" /></button>
                {sprint.state === 'Planned' && (
                  <button aria-label={`Delete ${sprint.name}`} onClick={() => remove(sprint)} disabled={!canPlan} title={planReason || undefined}
                    className="text-gray-400 hover:text-red-600 disabled:text-gray-300 disabled:hover:text-gray-300 disabled:cursor-not-allowed"><Trash2 className="h-4 w-4" /></button>
                )}
              </div>
            </header>
            {sprintItems.length === 0 ? (
              <p className="text-sm text-gray-400 text-center py-6">No items yet. Add some from the backlog below.</p>
            ) : (
              <ul className="divide-y divide-gray-100">
                {sprintItems.map((item) => (
                  <ItemRow key={item.id} item={item} sprints={otherSprints} currentSprintId={sprint.id} canPlan={canPlan} reason={planReason} onAssign={assign} />
                ))}
              </ul>
            )}
            {completing?.id === sprint.id && (
              <CompleteSprintModal sprint={sprint} unfinishedCount={unfinished} plannedSprints={planned.filter((p) => p.id !== sprint.id)} onComplete={complete} onClose={() => setCompleting(null)} />
            )}
          </section>
        );
      })}

      <section aria-label="Backlog" className="bg-white border border-gray-200 rounded-xl">
        <header className="px-4 py-3 border-b border-gray-100">
          <h3 className="text-base font-semibold text-gray-900">Backlog <span className="text-sm font-normal text-gray-500">· {backlogItems.length} unplanned item{backlogItems.length === 1 ? '' : 's'}</span></h3>
        </header>
        {backlogItems.length === 0 ? (
          <p className="text-sm text-gray-400 text-center py-6">Nothing waiting to be planned.</p>
        ) : (
          <ul className="divide-y divide-gray-100">
            {backlogItems.map((item) => (
              <ItemRow key={item.id} item={item} sprints={open} currentSprintId={null} canPlan={canPlan} reason={planReason} onAssign={assign} />
            ))}
          </ul>
        )}
      </section>

      <SprintEffort apiService={apiService} sprints={sprints} refreshKey={items} />

      <div className="grid gap-6 lg:grid-cols-2">
        <section aria-label="Burndown" className="bg-white border border-gray-200 rounded-xl p-4">
          <h3 className="text-base font-semibold text-gray-900 mb-2">Burndown {burndown && <span className="text-sm font-normal text-gray-500">· {burndown.name}</span>}</h3>
          {burndown ? <BurndownChart burndown={burndown} /> : <p className="text-sm text-gray-400 py-8 text-center">Start a sprint to see its burndown.</p>}
        </section>
        <section aria-label="Velocity" className="bg-white border border-gray-200 rounded-xl p-4">
          <h3 className="text-base font-semibold text-gray-900 mb-2">Velocity</h3>
          {velocity?.sprints.length ? <VelocityChart velocity={velocity} /> : <p className="text-sm text-gray-400 py-8 text-center">Complete a sprint to build velocity history.</p>}
        </section>
      </div>

      {completed.length > 0 && (
        <section aria-label="Completed sprints" className="bg-white border border-gray-200 rounded-xl">
          <header className="px-4 py-3 border-b border-gray-100"><h3 className="text-base font-semibold text-gray-900">Completed sprints</h3></header>
          <ul className="divide-y divide-gray-100">
            {[...completed].reverse().map((s) => (
              <li key={s.id} className="flex items-center gap-3 px-4 py-2.5 text-sm">
                <span className="font-medium text-gray-900 flex-1">{s.name}</span>
                <span className="text-gray-500">{fmtRange(s)}</span>
                <span className="text-gray-700">{s.completedPoints ?? 0} / {s.committedPoints ?? 0} pts</span>
                <button onClick={() => setRetroSprint(s)} className="px-2.5 py-1 rounded-lg border border-gray-300 text-xs text-gray-700 hover:bg-gray-50">Retro</button>
              </li>
            ))}
          </ul>
        </section>
      )}

      {retroSprint && <RetroModal apiService={apiService} sprint={retroSprint} canAdd={can(project.myRole, 'collaborate')} addReason={denyReason(project, 'collaborate')} onClose={() => setRetroSprint(null)} />}
      {editing !== undefined && <SprintModal sprint={editing} onSave={saveSprint} onClose={() => setEditing(undefined)} />}
    </div>
  );
};

export default PmSprints;
