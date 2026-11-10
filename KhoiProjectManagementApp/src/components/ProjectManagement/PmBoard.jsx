import React, { useState, useEffect, useCallback } from 'react';
import { Settings2, AlertTriangle, GitBranch } from 'lucide-react';
import LoadingSpinner from '../Common/LoadingSpinner';
import ErrorMessage from '../Common/ErrorMessage';
import PriorityBadge from '../Common/PriorityBadge';
import WorkItemModal from './WorkItemModal';
import BoardSettingsModal from './BoardSettingsModal';
import WorkflowModal from './WorkflowModal';
import { useToast } from '../../contexts/ToastContext';
import { reportApiError } from '../../utils/apiError';
import { TYPE_STYLES, can } from './pmConstants';

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
  const [assignee, setAssignee] = useState('');
  const [sprints, setSprints] = useState([]);
  const [sprintId, setSprintId] = useState('');
  const [workflow, setWorkflow] = useState(null);
  const [showWorkflow, setShowWorkflow] = useState(false);
  const [laneMode, setLaneMode] = useState('none');

  const canEdit = can(project.myRole, 'edit');
  const canConfigure = can(project.myRole, 'board');

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

  useEffect(() => { setLoading(true); load(); }, [load]);

  useEffect(() => {
    apiService.getSprints(project.id).then((s) => setSprints(s.filter((x) => x.state !== 'Completed'))).catch(() => setSprints([]));
  }, [apiService, project.id]);

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
  const visibleCards = assignee ? allCards.filter((i) => String(i.assignedToId) === assignee) : allCards;
  const lanes = buildLanes(laneMode, visibleCards);

  return (
    <div>
      <div className="flex flex-wrap items-center gap-2 mb-4">
        <select aria-label="Filter cards by assignee" value={assignee} onChange={(e) => setAssignee(e.target.value)}
          className="border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-blue-500">
          <option value="">Everyone</option>
          {members.map((m) => <option key={m.userId} value={m.userId}>{m.name}</option>)}
        </select>
        <select aria-label="Show work from" value={sprintId} onChange={(e) => setSprintId(e.target.value)}
          className="border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-blue-500">
          <option value="">All work (Kanban)</option>
          {sprints.map((s) => <option key={s.id} value={s.id}>{s.name}{s.state === 'Active' ? ' (active)' : ''}</option>)}
        </select>
        <select aria-label="Group cards into swimlanes" value={laneMode} onChange={(e) => setLaneMode(e.target.value)}
          className="border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-blue-500">
          {LANE_MODES.map((m) => <option key={m.key} value={m.key}>{m.label}</option>)}
        </select>
        <span className="text-xs text-gray-500">{allCards.length} cards on the board · Epics and Features stay on the backlog</span>
        {canConfigure && workflow && (
          <button onClick={() => setShowWorkflow(true)} className="ml-auto inline-flex items-center gap-1.5 px-3 py-2 rounded-lg border border-gray-300 text-sm text-gray-700 hover:bg-gray-50">
            <GitBranch className="h-4 w-4" aria-hidden="true" /> Workflow{workflow.isCustomised ? ' (custom)' : ''}
          </button>
        )}
        {canConfigure && (
          <button onClick={() => setShowSettings(true)} className={`${workflow ? '' : 'ml-auto '}inline-flex items-center gap-1.5 px-3 py-2 rounded-lg border border-gray-300 text-sm text-gray-700 hover:bg-gray-50`}>
            <Settings2 className="h-4 w-4" aria-hidden="true" /> Columns &amp; WIP limits
          </button>
        )}
      </div>

      {lanes.length === 0 && <p className="text-sm text-gray-500 py-6 text-center">No cards to group.</p>}
      {lanes.map((lane) => (
      <div key={lane.key} className="mb-2">
      {lane.label && <h3 className="text-sm font-semibold text-gray-700 mb-2 px-1">{lane.label} <span className="font-normal text-gray-500">({visibleCards.filter(lane.match).length})</span></h3>}
      <div className="flex gap-4 overflow-x-auto pb-4">
        {board.columns.map((col) => {
          const cards = col.items.filter((i) => (!assignee || String(i.assignedToId) === assignee) && lane.match(i));
          const atLimit = col.wipLimit != null && col.count >= col.wipLimit;
          return (
            <section
              key={col.id}
              aria-label={`${col.name} column`}
              onDragOver={(e) => { if (drag && canEdit && moveAllowed(drag.fromStatus, col.status)) { e.preventDefault(); setOverStatus(col.status); } }}
              onDragLeave={() => setOverStatus((s) => (s === col.status ? null : s))}
              onDrop={(e) => { e.preventDefault(); dropOn(col.status, null); }}
              className={`w-72 flex-shrink-0 rounded-xl bg-gray-100 border ${overStatus === col.status ? 'border-blue-400 ring-2 ring-blue-200' : 'border-transparent'}`}
            >
              <header className="flex items-center justify-between px-3 py-2.5">
                <h3 className="text-sm font-semibold text-gray-800">{col.name}</h3>
                <span className={`inline-flex items-center gap-1 text-xs font-semibold rounded-full px-2 py-0.5 ${col.overWip ? 'bg-red-100 text-red-700' : atLimit ? 'bg-amber-100 text-amber-700' : 'bg-white text-gray-600'}`}
                  title={`${laneMode === 'none' ? '' : `${cards.length} in this lane · `}${col.count} in the column${col.wipLimit != null ? ` · WIP limit ${col.wipLimit}` : ' · no WIP limit'}`}>
                  {(col.overWip || atLimit) && <AlertTriangle className="h-3 w-3" aria-hidden="true" />}
                  {laneMode === 'none' ? col.count : cards.length}{laneMode === 'none' && col.wipLimit != null && ` / ${col.wipLimit}`}
                </span>
              </header>

              <ul className="px-2 pb-2 space-y-2 min-h-[4rem]">
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
                      className={`bg-white rounded-lg border border-gray-200 shadow-sm p-3 ${canEdit ? 'cursor-grab' : ''} ${drag?.id === item.id ? 'opacity-40' : ''}`}
                    >
                      <button onClick={() => setEditing(item)} className="block w-full text-left">
                        <span className="block text-sm font-medium text-gray-900 break-words">{item.title}</span>
                      </button>
                      <div className="flex flex-wrap items-center gap-1.5 mt-2">
                        <span className={`px-1.5 py-0.5 rounded text-[10.5px] font-semibold ${TYPE_STYLES[item.type] || 'bg-gray-100 text-gray-700'}`}>{item.type}</span>
                        <PriorityBadge priority={item.priority} />
                        {item.storyPoints != null && <span className="text-[11px] font-semibold bg-gray-100 text-gray-700 rounded-full px-2 py-0.5" title="Story points">{item.storyPoints}</span>}
                        {item.isOverdue && <span className="text-[11px] font-medium text-red-600">overdue</span>}
                      </div>
                      <div className="flex items-center justify-between mt-2 text-xs text-gray-500">
                        <span className="truncate">{item.assignedToName || 'Unassigned'}</span>
                        {timing && <span title="Time since work started">{timing}</span>}
                      </div>
                      {canEdit && (
                        <label className="block mt-2">
                          <span className="sr-only">Move {item.title} to</span>
                          <select value={item.status} onChange={(e) => move(item.id, e.target.value, null)}
                            className="w-full border border-gray-200 rounded px-2 py-1 text-xs bg-white text-gray-600">
                            {board.columns.map((c) => <option key={c.status} value={c.status} disabled={!moveAllowed(item.status, c.status)}>{c.name}</option>)}
                          </select>
                        </label>
                      )}
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

      {showWorkflow && workflow && <WorkflowModal workflow={workflow} onSave={saveWorkflow} onClose={() => setShowWorkflow(false)} />}
      {editing && (
        <WorkItemModal item={editing} items={allCards} members={members} canEdit={canEdit} apiService={apiService} role={project.myRole} onSave={saveItem} onClose={() => setEditing(null)} />
      )}
      {showSettings && (
        <BoardSettingsModal columns={board.columns} onSave={saveColumns} onClose={() => setShowSettings(false)} />
      )}
    </div>
  );
};

export default PmBoard;
