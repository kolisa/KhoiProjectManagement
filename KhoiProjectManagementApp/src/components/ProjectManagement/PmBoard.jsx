import React, { useState, useEffect, useCallback, useRef } from 'react';
import { Settings2, AlertTriangle, GitBranch, Plus, Play, Target, UserRound } from 'lucide-react';
import LoadingSpinner from '../Common/LoadingSpinner';
import ErrorMessage from '../Common/ErrorMessage';
import PriorityBadge from '../Common/PriorityBadge';
import WorkItemModal from './WorkItemModal';
import BoardSettingsModal from './BoardSettingsModal';
import WorkflowModal from './WorkflowModal';
import { SprintModal } from './SprintModals';
import Combobox from './Combobox';
import { Avatar } from './pmUi';
import { LabelChips } from './pmWidgets';
import { useToast } from '../../contexts/ToastContext';
import { reportApiError } from '../../utils/apiError';
import { TYPE_STYLES, STATUS_DOT, can, denyReason } from './pmConstants';
import { Button, Select, Badge } from '../ui';

const DAY_MS = 86400000;

// Swimlanes group the same cards into horizontal bands; the columns inside each band are unchanged.
const LANE_MODES = [
  { key: 'none', label: 'No swimlanes' },
  { key: 'assignee', label: 'By assignee' },
  { key: 'priority', label: 'By priority' },
  { key: 'type', label: 'By type' },
];
const PRIORITY_ORDER = ['high', 'medium', 'low'];
const cap = (s) => s.charAt(0).toUpperCase() + s.slice(1);

const buildLanes = (mode, cards) => {
  if (mode === 'none') return [{ key: 'all', label: null, match: () => true }];
  // People are grouped by id, not display name, so two people who share a name get separate lanes.
  const keyOf = { assignee: (i) => String(i.assignedToId ?? 'none'), priority: (i) => i.priority, type: (i) => i.type }[mode];
  const labelOf = (k) => {
    if (mode === 'priority') return `${cap(k)} priority`;
    if (mode === 'assignee') return cards.find((i) => keyOf(i) === k)?.assignedToName || 'Unassigned';
    return k;
  };
  const keys = [...new Set(cards.map(keyOf))];
  if (mode === 'priority') keys.sort((a, b) => PRIORITY_ORDER.indexOf(a) - PRIORITY_ORDER.indexOf(b));
  else keys.sort((a, b) => (a === 'none') - (b === 'none') || labelOf(a).localeCompare(labelOf(b)));
  return keys.map((k) => ({ key: k, label: labelOf(k), match: (i) => keyOf(i) === k }));
};

// Cycle time for finished cards (started -> done), age-in-column feel for the rest. Legacy cards that
// predate status tracking have no StartedAt, so they simply show nothing rather than a made-up number.
const cardTiming = (item) => {
  if (!item.startedAt) return null;
  if (item.completedAt) {
    const days = Math.max(0, Math.round((new Date(item.completedAt) - new Date(item.startedAt)) / DAY_MS));
    return `${days}d cycle`;
  }
  const days = Math.max(0, Math.floor((Date.now() - new Date(item.startedAt)) / DAY_MS));
  return `${days}d in flight`;
};

// Kanban board: one column per status with WIP limits, native drag-and-drop between and within
// columns, and a keyboard/touch-friendly "Move to" select on every card (drag-and-drop alone is not
// accessible). The server is authoritative - a refused move (WIP limit, permissions) reloads the board.
const PmBoard = ({ apiService, project, members }) => {
  const toast = useToast();
  const [board, setBoard] = useState(null);
  const [error, setError] = useState(null);
  const [loading, setLoading] = useState(true);
  const [drag, setDrag] = useState(null); // { id, fromStatus }
  const [overStatus, setOverStatus] = useState(null);
  const [editing, setEditing] = useState(null);
  const [showSettings, setShowSettings] = useState(false);
  const [people, setPeople] = useState(new Set());   // person filter: user ids as strings, or 'none' for unassigned
  const [sprints, setSprints] = useState([]);
  const [sprintId, setSprintId] = useState('');
  const [readyFor, setReadyFor] = useState(null);   // the project id whose sprints have been loaded (and default sprint chosen)
  const sprintsReady = readyFor === project.id;
  const [newSprint, setNewSprint] = useState(false);
  const picked = useRef(false);   // true once the person has chosen a sprint (or All work) themselves
  const [workflow, setWorkflow] = useState(null);
  const [showWorkflow, setShowWorkflow] = useState(false);
  const [laneMode, setLaneMode] = useState('none');

  const canEdit = can(project.myRole, 'edit');
  const canConfigure = can(project.myRole, 'board');
  const configureReason = denyReason(project, 'board');
  const editReason = denyReason(project, 'edit');
  const canPlan = can(project.myRole, 'sprint');
  const planReason = denyReason(project, 'sprint');

  const load = useCallback(async () => {
    try {
      setError(null);
      setBoard(await apiService.getBoard(project.id, sprintId || null));
    } catch (err) {
      setError(err?.message || 'Could not load the board.');
    } finally {
      setLoading(false);
    }
  }, [apiService, project.id, sprintId]);

  useEffect(() => { if (sprintsReady) { setLoading(true); load(); } }, [load, sprintsReady]);

  // Sprints for the picker. Like a Scrum board in Jira, the board opens on the ACTIVE sprint; with none active it shows all
  // work (plain Kanban). Choosing something yourself always wins.
  const loadSprints = useCallback(async (select) => {
    let open = [];
    try { open = (await apiService.getSprints(project.id)).filter((x) => x.state !== 'Completed'); } catch { open = []; }
    setSprints(open);
    if (select != null) { picked.current = true; setSprintId(String(select)); }
    else if (!picked.current) setSprintId(String(open.find((s) => s.state === 'Active')?.id ?? ''));
    setReadyFor(project.id);
  }, [apiService, project.id]);

  useEffect(() => {
    // A different project: forget the previous project's sprint choice and people filter before anything loads.
    picked.current = false;
    setSprintId('');
    setPeople(new Set());
    loadSprints();
  }, [loadSprints]);

  const createSprint = async (dto) => {
    const created = await apiService.createSprint(project.id, dto);
    toast.success('Sprint created.');
    setNewSprint(false);
    await loadSprints(created?.id);
  };

  const startSprint = async (id) => {
    try {
      await apiService.startSprint(id);
      toast.success('Sprint started.');
      await loadSprints(id);
      await load();
    } catch (err) { reportApiError(toast, err, 'Could not start the sprint.'); }
  };

  const togglePerson = (key) => setPeople((p) => { const n = new Set(p); if (n.has(key)) n.delete(key); else n.add(key); return n; });

  const loadWorkflow = useCallback(() => {
    apiService.getWorkflow(project.id).then(setWorkflow).catch(() => setWorkflow(null));
  }, [apiService, project.id]);

  useEffect(() => { loadWorkflow(); }, [loadWorkflow]);

  // Mirrors the server's rule so impossible moves are not offered; the server still has the final say.
  const moveAllowed = (from, to) =>
    from === to || !workflow || workflow.transitions.find((t) => t.from === from && t.to === to)?.allowed !== false;

  const saveWorkflow = async (transitions) => {
    setWorkflow(await apiService.saveWorkflow(project.id, transitions));
    toast.success('Workflow updated.');
    setShowWorkflow(false);
  };

  const move = async (itemId, status, afterItemId) => {
    try {
      await apiService.moveWorkItem(itemId, status, afterItemId ?? null);
    } catch (err) {
      reportApiError(toast, err, 'Could not move the card.');
    }
    await load(); // always resync - on success for the new counts/ranks, on failure to undo
  };

  const dropOn = (status, afterItemId) => {
    if (drag && canEdit) move(drag.id, status, afterItemId);
    setDrag(null);
    setOverStatus(null);
  };

  const saveItem = async (dto) => {
    await apiService.updateWorkItem(editing.id, dto);
    toast.success('Work item updated.');
    setEditing(null);
    await load();
  };

  const saveColumns = async (columns) => {
    setBoard(await apiService.saveBoardColumns(project.id, columns));
    toast.success('Board updated.');
    setShowSettings(false);
  };

  if (loading) return <LoadingSpinner />;
  if (error) return <ErrorMessage message={error} onRetry={load} />;

  const allCards = board.columns.flatMap((c) => c.items);
  const personKey = (i) => String(i.assignedToId ?? 'none');
  const matchPerson = (i) => people.size === 0 || people.has(personKey(i));
  const visibleCards = allCards.filter(matchPerson);
  const sprint = sprints.find((s) => String(s.id) === sprintId);
  const hasActive = sprints.some((s) => s.state === 'Active');
  const countFor = (key) => allCards.filter((i) => personKey(i) === key).length;
  // Everyone on the board, like the avatar row on a Jira board - not just whoever happens to have a card right now.
  const boardPeople = members.filter((m) => countFor(String(m.userId)) > 0);
  const unassignedCount = countFor('none');
  const daysLeft = sprint?.state === 'Active' ? Math.ceil((new Date(sprint.endDate) - Date.now()) / DAY_MS) : null;
  const lanes = buildLanes(laneMode, visibleCards);

  return (
    <div>
      <div className="flex flex-wrap items-center gap-2 mb-4">
        <label htmlFor="board-sprint" className="sr-only">Show work from</label>
        <Combobox id="board-sprint" className="w-56" value={sprintId} searchPlaceholder="Search sprints…"
          onChange={(v) => { picked.current = true; setSprintId(String(v)); }}
          emptyLabel="All work (Kanban)" options={sprints.map((s) => ({ value: String(s.id), label: s.name, description: s.state === 'Active' ? 'Active sprint' : 'Planned' }))} />
        <Button variant="outline" onClick={() => setNewSprint(true)} disabled={!canPlan} title={planReason || 'Create a sprint to plan work into'}>
          <Plus className="h-4 w-4" aria-hidden="true" /> New sprint
        </Button>
        <Select aria-label="Group cards into swimlanes" value={laneMode} onChange={(e) => setLaneMode(e.target.value)} className="w-auto">
          {LANE_MODES.map((m) => <option key={m.key} value={m.key}>{m.label}</option>)}
        </Select>
        <span className="text-xs text-gray-500">{allCards.length} card{allCards.length === 1 ? '' : 's'} · Epics and Features stay on the backlog</span>
        {workflow && (
          <Button variant="outline" onClick={() => setShowWorkflow(true)} disabled={!canConfigure} title={configureReason || undefined} className="ml-auto">
            <GitBranch className="h-4 w-4" aria-hidden="true" /> Workflow{workflow.isCustomised ? ' (custom)' : ''}
          </Button>
        )}
        <Button variant="outline" onClick={() => setShowSettings(true)} disabled={!canConfigure} title={configureReason || undefined} className={workflow ? '' : 'ml-auto'}>
          <Settings2 className="h-4 w-4" aria-hidden="true" /> Columns &amp; WIP limits
        </Button>
      </div>

      {sprint && (
        <div role="region" aria-label={`${sprint.name} summary`} className="mb-4 flex flex-wrap items-center gap-x-4 gap-y-1 rounded-xl border border-blue-100 bg-blue-50/60 px-4 py-2.5">
          <span className="text-sm font-semibold text-gray-900">{sprint.name}</span>
          <Badge variant={sprint.state === 'Active' ? 'success' : 'secondary'} className="text-[11px] font-semibold">{sprint.state}</Badge>
          <span className="text-xs text-gray-600">{new Date(sprint.startDate).toLocaleDateString(undefined, { month: 'short', day: 'numeric' })} – {new Date(sprint.endDate).toLocaleDateString(undefined, { month: 'short', day: 'numeric' })}{daysLeft != null && ` · ${daysLeft > 0 ? `${daysLeft} day${daysLeft === 1 ? '' : 's'} left` : 'ends today or overdue'}`}</span>
          <span className="text-xs text-gray-600">{sprint.donePoints} / {sprint.totalPoints} pts done</span>
          {sprint.goal && <span className="flex items-center gap-1 text-xs text-gray-700"><Target className="h-3.5 w-3.5 text-blue-500" aria-hidden="true" />{sprint.goal}</span>}
          {sprint.state === 'Planned' && (
            <Button size="sm" onClick={() => startSprint(sprint.id)} disabled={!canPlan || hasActive} title={planReason || (hasActive ? 'Complete the active sprint first' : 'Start this sprint')} className="ml-auto">
              <Play className="h-3.5 w-3.5" aria-hidden="true" /> Start sprint
            </Button>
          )}
        </div>
      )}

      {(boardPeople.length > 0 || unassignedCount > 0) && (
        <div role="group" aria-label="Filter by person" className="mb-4 flex flex-wrap items-center gap-1">
          <span className="mr-1 text-xs font-medium text-gray-500">People</span>
          <div className="flex items-center -space-x-1.5 hover:space-x-0.5 transition-all">
            {boardPeople.map((m) => {
              const key = String(m.userId); const on = people.has(key);
              return (
                <button key={m.userId} type="button" aria-pressed={on} aria-label={`${m.name}, ${countFor(key)} card${countFor(key) === 1 ? '' : 's'}`} title={`${m.name} · ${countFor(key)} card${countFor(key) === 1 ? '' : 's'}`}
                  onClick={() => togglePerson(key)}
                  className={`relative rounded-full ring-2 transition ${on ? 'ring-blue-600 z-10' : 'ring-white hover:ring-blue-200'}`}>
                  <Avatar name={m.name} />
                </button>
              );
            })}
          </div>
          {unassignedCount > 0 && (
            <Button variant="outline" size="sm" aria-pressed={people.has('none')} aria-label={`Unassigned, ${unassignedCount} card${unassignedCount === 1 ? '' : 's'}`} onClick={() => togglePerson('none')}
              className={`ml-1 h-auto gap-1 rounded-full px-2.5 py-1 font-normal ${people.has('none') ? 'border-blue-600 bg-blue-50 text-blue-700' : 'border-dashed border-gray-300 text-gray-600'}`}>
              <UserRound className="h-3.5 w-3.5" aria-hidden="true" /> Unassigned
            </Button>
          )}
          {people.size > 0 && <Button variant="link" size="sm" onClick={() => setPeople(new Set())} className="ml-1 h-auto px-0 text-xs text-blue-700">Clear filter</Button>}
        </div>
      )}

      {allCards.length === 0 && sprint && (
        <p className="mb-4 rounded-xl border border-dashed border-gray-300 px-4 py-6 text-center text-sm text-gray-500">
          No work in {sprint.name} yet. Add items from the Backlog or the Sprints tab, then they appear here.
        </p>
      )}
      {lanes.length === 0 && allCards.length > 0 && <p className="text-sm text-gray-500 py-6 text-center">No cards match this filter.</p>}
      {lanes.map((lane) => (
      <div key={lane.key} className="mb-2">
      {lane.label && (
        <h3 className="flex items-center gap-2 text-sm font-semibold text-gray-700 mb-2 px-1">
          {laneMode === 'assignee' && (lane.key === 'none' ? <UserRound className="h-5 w-5 text-gray-400" aria-hidden="true" /> : <Avatar name={lane.label} size="sm" />)}
          {lane.label} <span className="font-normal text-gray-500">({visibleCards.filter(lane.match).length})</span>
        </h3>
      )}
      <div className="flex gap-4 overflow-x-auto pb-4">
        {board.columns.map((col) => {
          const cards = col.items.filter((i) => matchPerson(i) && lane.match(i));
          const atLimit = col.wipLimit != null && col.count >= col.wipLimit;
          return (
            <section
              key={col.id}
              aria-label={`${col.name} column`}
              onDragOver={(e) => { if (drag && canEdit && moveAllowed(drag.fromStatus, col.status)) { e.preventDefault(); setOverStatus(col.status); } }}
              onDragLeave={() => setOverStatus((s) => (s === col.status ? null : s))}
              onDrop={(e) => { e.preventDefault(); dropOn(col.status, null); }}
              className={`w-72 shrink-0 rounded-xl bg-gray-50 border ${overStatus === col.status ? 'border-blue-400 ring-2 ring-blue-200' : 'border-gray-200'}`}
            >
              <header className="flex items-center justify-between px-3 py-2.5">
                <h3 className="flex items-center gap-2 text-sm font-semibold text-gray-800"><span className={`h-2.5 w-2.5 rounded-full ${STATUS_DOT[col.status] || 'bg-gray-400'}`} aria-hidden="true" />{col.name}</h3>
                <Badge variant={col.overWip ? 'destructive' : atLimit ? 'warning' : 'secondary'} className="font-semibold"
                  title={`${laneMode === 'none' ? '' : `${cards.length} in this lane · `}${col.count} in the column${col.wipLimit != null ? ` · WIP limit ${col.wipLimit}` : ' · no WIP limit'}`}>
                  {(col.overWip || atLimit) && <AlertTriangle className="h-3 w-3" aria-hidden="true" />}
                  {laneMode === 'none' ? col.count : cards.length}{laneMode === 'none' && col.wipLimit != null && ` / ${col.wipLimit}`}
                </Badge>
              </header>

              <ul className="px-2 pb-2 space-y-2 min-h-16">
                {cards.map((item) => {
                  const timing = cardTiming(item);
                  return (
                    <li
                      key={item.id}
                      draggable={canEdit}
                      onDragStart={() => setDrag({ id: item.id, fromStatus: item.status })}
                      onDragEnd={() => { setDrag(null); setOverStatus(null); }}
                      onDragOver={(e) => { if (drag && drag.id !== item.id && canEdit && moveAllowed(drag.fromStatus, col.status)) e.preventDefault(); }}
                      onDrop={(e) => { e.preventDefault(); e.stopPropagation(); if (drag && drag.id !== item.id) dropOn(col.status, item.id); }}
                      className={`bg-white rounded-lg border border-gray-200 shadow-xs p-3 transition-shadow hover:border-gray-300 hover:shadow-md ${canEdit ? 'cursor-grab' : ''} ${drag?.id === item.id ? 'opacity-40' : ''}`}
                    >
                      <button onClick={() => setEditing(item)} className="block w-full text-left">
                        <span className="block text-sm font-medium text-gray-900 wrap-break-word">{item.title}</span>
                      </button>
                      <div className="flex flex-wrap items-center gap-1.5 mt-2">
                        <Badge variant="secondary" className={`rounded-sm px-1.5 text-[10.5px] font-semibold ${TYPE_STYLES[item.type] || 'bg-gray-100 text-gray-700'}`}>{item.type}</Badge>
                        <PriorityBadge priority={item.priority} />
                        {item.storyPoints != null && <Badge variant="secondary" className="bg-gray-100 text-[11px] font-semibold text-gray-700" title="Story points">{item.storyPoints}</Badge>}
                        {item.isOverdue && <span className="text-[11px] font-medium text-red-600">overdue</span>}
                      </div>
                      {item.labels?.length > 0 && <div className="mt-2"><LabelChips labels={item.labels} max={2} /></div>}
                      <div className="flex items-center justify-between gap-2 mt-2 text-xs text-gray-500">
                        <span className="flex min-w-0 items-center gap-2">
                          <span className="font-medium text-gray-400">#{item.id}</span>
                          {timing && <span title="Time since work started">{timing}</span>}
                        </span>
                        {item.assignedToId != null ? (
                          <span className="shrink-0" title={item.assignedToName}>
                            <Avatar name={item.assignedToName || '?'} size="sm" />
                            <span className="sr-only">Assigned to {item.assignedToName}</span>
                          </span>
                        ) : (
                          <span className="shrink-0 inline-flex h-7 w-7 items-center justify-center rounded-full border border-dashed border-gray-300 text-gray-400" title="Unassigned">
                            <UserRound className="h-3.5 w-3.5" aria-hidden="true" /><span className="sr-only">Unassigned</span>
                          </span>
                        )}
                      </div>
                      <label className="block mt-2">
                        <span className="sr-only">Move {item.title} to</span>
                        <Select value={item.status} onChange={(e) => move(item.id, e.target.value, null)} disabled={!canEdit} title={editReason || undefined}
                          className="h-auto rounded-sm border-gray-200 px-2 py-1 text-xs text-gray-600">
                          {board.columns.map((c) => <option key={c.status} value={c.status} disabled={!moveAllowed(item.status, c.status)}>{c.name}</option>)}
                        </Select>
                      </label>
                    </li>
                  );
                })}
                {cards.length === 0 && <li className="text-xs text-gray-400 text-center py-4">No cards</li>}
              </ul>
            </section>
          );
        })}
      </div>
      </div>
      ))}

      {newSprint && <SprintModal sprint={null} onSave={createSprint} onClose={() => setNewSprint(false)} />}
      {showWorkflow && workflow && <WorkflowModal workflow={workflow} onSave={saveWorkflow} onClose={() => setShowWorkflow(false)} />}
      {editing && (
        <WorkItemModal item={editing} items={allCards} members={members} canEdit={canEdit} apiService={apiService} project={project} role={project.myRole} onChanged={load} onSave={saveItem} onClose={() => setEditing(null)} />
      )}
      {showSettings && (
        <BoardSettingsModal columns={board.columns} onSave={saveColumns} onClose={() => setShowSettings(false)} />
      )}
    </div>
  );
};

export default PmBoard;
