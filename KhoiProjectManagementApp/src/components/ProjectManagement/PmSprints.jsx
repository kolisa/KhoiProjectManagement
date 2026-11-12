import React, { useState, useEffect, useCallback, useMemo } from 'react';
import { Plus, Play, CheckCircle2, Pencil, Trash2, Target, GripVertical, X } from 'lucide-react';
import LoadingSpinner from '../Common/LoadingSpinner';
import ErrorMessage from '../Common/ErrorMessage';
import StatusBadge from '../Common/StatusBadge';
import { SprintModal, CompleteSprintModal } from './SprintModals';
import RetroModal from './RetroModal';
import SprintEffort from './SprintEffort';
import Combobox from './Combobox';
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

// One planned item. Tick it to move several at once, drag it onto any sprint (or the backlog), or use "Move to".
const ItemRow = ({ item, sprints, currentSprintId, canPlan, reason, selected, onSelect, dragging, onDragStart, onDragEnd, onAssign }) => (
  <li draggable={canPlan} onDragStart={onDragStart} onDragEnd={onDragEnd}
    className={`flex items-center gap-3 px-3 py-2 hover:bg-gray-50 ${dragging ? 'opacity-40' : ''} ${selected ? 'bg-blue-50/60' : ''}`}>
    <input type="checkbox" aria-label={`Select ${item.title}`} checked={selected} onChange={onSelect} disabled={!canPlan} title={reason || undefined}
      className="h-4 w-4 rounded-sm border-gray-300 disabled:cursor-not-allowed" />
    <GripVertical className={`h-4 w-4 ${canPlan ? 'cursor-grab text-gray-300' : 'text-gray-200'}`} aria-hidden="true" />
    <span className={`px-2 py-0.5 rounded-sm text-[11px] font-semibold ${TYPE_STYLES[item.type] || 'bg-gray-100 text-gray-700'}`}>{item.type}</span>
    <span className="flex-1 min-w-0 text-sm text-gray-900 truncate">{item.title}</span>
    {item.assignedToName && <span className="hidden md:inline text-xs text-gray-500 w-28 truncate">{item.assignedToName}</span>}
    {item.storyPoints != null && <span className="text-xs font-semibold bg-gray-100 text-gray-700 rounded-full px-2 py-0.5" title="Story points">{item.storyPoints}</span>}
    <StatusBadge status={item.status} label={statusLabel(item.status)} colorMap={STATUS_STYLES} />
    <div className="w-40">
      <label htmlFor={`move-${item.id}`} className="sr-only">Sprint for {item.title}</label>
      <Combobox id={`move-${item.id}`} size="sm" value={currentSprintId ?? ''} disabled={!canPlan} title={reason || undefined}
        onChange={(v) => onAssign(item, v === '' ? null : Number(v))} emptyLabel="Backlog" searchPlaceholder="Search sprints…"
        options={sprints.map((sp) => ({ value: sp.id, label: sp.name, description: sp.state === 'Active' ? 'Active' : 'Planned' }))} />
    </div>
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
  const [selected, setSelected] = useState(new Set());
  const [dragId, setDragId] = useState(null);
  const [dropOver, setDropOver] = useState(null);   // 'backlog' or a sprint id

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

  const toggle = (id) => setSelected((p) => { const n = new Set(p); if (n.has(id)) n.delete(id); else n.add(id); return n; });
  const nameOfTarget = (sprintId) => (sprintId == null ? 'the backlog' : sprints.find((x) => x.id === sprintId)?.name || 'the sprint');

  // Moves several items one after another (the server checks each: completed sprints, container types...). Reports how many
  // made it, so a partial failure is never silent.
  const moveItems = async (ids, sprintId) => {
    let moved = 0;
    try {
      for (const id of ids) { await apiService.assignWorkItemSprint(id, sprintId); moved += 1; }
      toast.success(`Moved ${moved} item${moved === 1 ? '' : 's'} to ${nameOfTarget(sprintId)}.`);
    } catch (err) {
      reportApiError(toast, err, `Moved ${moved} of ${ids.length}, then something went wrong.`);
    }
    setSelected(new Set());
    await load();
  };

  const dropOn = (target) => {
    const ids = (selected.has(dragId) ? [...selected] : [dragId]).filter((id) => (items.find((i) => i.id === id)?.sprintId ?? null) !== target);
    setDragId(null);
    setDropOver(null);
    if (dragId != null && canPlan && ids.length > 0) moveItems(ids, target);
  };
  const dropProps = (target) => ({
    onDragOver: (e) => { if (dragId != null && canPlan) { e.preventDefault(); setDropOver(target ?? 'backlog'); } },
    onDragLeave: () => setDropOver((d) => (d === (target ?? 'backlog') ? null : d)),
    onDrop: (e) => { e.preventDefault(); dropOn(target); },
  });
  const rowProps = (item) => ({
    selected: selected.has(item.id), onSelect: () => toggle(item.id), dragging: dragId === item.id,
    onDragStart: () => setDragId(item.id), onDragEnd: () => { setDragId(null); setDropOver(null); },
  });

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

      {selected.size > 0 && (
        <div role="region" aria-label="Move selected items" className="sticky top-2 z-20 flex flex-wrap items-center gap-3 rounded-xl border border-blue-200 bg-blue-50 px-4 py-2.5 shadow-xs">
          <span className="text-sm font-medium text-blue-900">{selected.size} selected</span>
          <label htmlFor="bulk-move" className="sr-only">Move selected items to</label>
          <Combobox id="bulk-move" className="w-56" value="__none__" placeholder="Move selected to…" searchPlaceholder="Search sprints…"
            onChange={(v) => moveItems([...selected], v === '' ? null : Number(v))} emptyLabel="Backlog"
            options={open.map((sp) => ({ value: sp.id, label: sp.name, description: sp.state === 'Active' ? 'Active' : 'Planned' }))} />
          <button type="button" onClick={() => setSelected(new Set())} className="ml-auto inline-flex items-center gap-1 text-xs font-medium text-blue-800 hover:underline"><X className="h-3.5 w-3.5" aria-hidden="true" /> Clear selection</button>
        </div>
      )}

      {open.map((sprint) => {
        const sprintItems = items.filter((i) => i.sprintId === sprint.id);
        const unfinished = sprintItems.filter((i) => i.status !== 'completed').length;
        const over = sprint.capacity != null && sprint.totalPoints > sprint.capacity;
        const pct = sprint.capacity ? Math.min(100, Math.round((sprint.totalPoints / sprint.capacity) * 100)) : 0;
        const otherSprints = open;
        return (
          <section key={sprint.id} aria-label={sprint.name} {...dropProps(sprint.id)}
            className={`bg-white border rounded-xl ${dropOver === sprint.id ? 'border-blue-400 ring-2 ring-blue-200' : 'border-gray-200'}`}>
            <header className="flex flex-wrap items-start gap-3 p-4 border-b border-gray-100">
              <div className="flex-1 min-w-[200px]">
                <div className="flex items-center gap-2">
                  <h3 className="text-base font-semibold text-gray-900">{sprint.name}</h3>
                  <span className={`text-[11px] font-semibold rounded-sm px-2 py-0.5 ${sprint.state === 'Active' ? 'bg-green-100 text-green-700' : 'bg-gray-100 text-gray-600'}`}>{sprint.state}</span>
                </div>
                <p className="text-xs text-gray-500 mt-0.5">{fmtRange(sprint)} · {sprint.itemCount} item{sprint.itemCount === 1 ? '' : 's'}</p>
                {sprint.goal && <p className="text-sm text-gray-700 mt-2 flex gap-1.5"><Target className="h-4 w-4 text-blue-500 shrink-0 mt-0.5" aria-hidden="true" />{sprint.goal}</p>}
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
              <p className="text-sm text-gray-400 text-center py-6">No items yet. Drag items here from the backlog below, or tick them and use &ldquo;Move selected to&hellip;&rdquo;.</p>
            ) : (
              <ul className="divide-y divide-gray-100">
                {sprintItems.map((item) => (
                  <ItemRow key={item.id} item={item} sprints={otherSprints} currentSprintId={sprint.id} canPlan={canPlan} reason={planReason} onAssign={assign} {...rowProps(item)} />
                ))}
              </ul>
            )}
            {completing?.id === sprint.id && (
              <CompleteSprintModal sprint={sprint} unfinishedCount={unfinished} plannedSprints={planned.filter((p) => p.id !== sprint.id)} onComplete={complete} onClose={() => setCompleting(null)} />
            )}
          </section>
        );
      })}

      <section aria-label="Backlog" {...dropProps(null)}
        className={`bg-white border rounded-xl ${dropOver === 'backlog' ? 'border-blue-400 ring-2 ring-blue-200' : 'border-gray-200'}`}>
        <header className="flex items-center gap-3 px-4 py-3 border-b border-gray-100">
          {backlogItems.length > 0 && (
            <input type="checkbox" aria-label="Select all backlog items" disabled={!canPlan} className="h-4 w-4 rounded-sm border-gray-300 disabled:cursor-not-allowed"
              checked={backlogItems.every((i) => selected.has(i.id))}
              onChange={(e) => setSelected((p) => { const n = new Set(p); backlogItems.forEach((i) => (e.target.checked ? n.add(i.id) : n.delete(i.id))); return n; })} />
          )}
          <h3 className="text-base font-semibold text-gray-900">Backlog <span className="text-sm font-normal text-gray-500">· {backlogItems.length} unplanned item{backlogItems.length === 1 ? '' : 's'}</span></h3>
          {canPlan && backlogItems.length > 0 && <span className="ml-auto text-xs text-gray-500">Drag onto a sprint, or tick items and move them together</span>}
        </header>
        {backlogItems.length === 0 ? (
          <p className="text-sm text-gray-400 text-center py-6">Nothing waiting to be planned.</p>
        ) : (
          <ul className="divide-y divide-gray-100">
            {backlogItems.map((item) => (
              <ItemRow key={item.id} item={item} sprints={open} currentSprintId={null} canPlan={canPlan} reason={planReason} onAssign={assign} {...rowProps(item)} />
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
