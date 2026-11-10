import React, { useState, useEffect, useCallback } from 'react';
import { Settings2, AlertTriangle } from 'lucide-react';
import LoadingSpinner from '../Common/LoadingSpinner';
import ErrorMessage from '../Common/ErrorMessage';
import PriorityBadge from '../Common/PriorityBadge';
import WorkItemModal from './WorkItemModal';
import BoardSettingsModal from './BoardSettingsModal';
import { useToast } from '../../contexts/ToastContext';
import { reportApiError } from '../../utils/apiError';
import { TYPE_STYLES, can } from './pmConstants';

const DAY_MS = 86400000;

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

  const canEdit = can(project.myRole, 'edit');
  const canConfigure = can(project.myRole, 'board');

  const load = useCallback(async () => {
    try {
      setError(null);
      setBoard(await apiService.getBoard(project.id));
    } catch (err) {
      setError(err?.message || 'Could not load the board.');
    } finally {
      setLoading(false);
    }
  }, [apiService, project.id]);

  useEffect(() => { setLoading(true); load(); }, [load]);

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

  return (
    <div>
      <div className="flex flex-wrap items-center gap-2 mb-4">
        <select aria-label="Filter cards by assignee" value={assignee} onChange={(e) => setAssignee(e.target.value)}
          className="border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-blue-500">
          <option value="">Everyone</option>
          {members.map((m) => <option key={m.userId} value={m.userId}>{m.name}</option>)}
        </select>
        <span className="text-xs text-gray-500">{allCards.length} cards on the board · Epics and Features stay on the backlog</span>
        {canConfigure && (
          <button onClick={() => setShowSettings(true)} className="ml-auto inline-flex items-center gap-1.5 px-3 py-2 rounded-lg border border-gray-300 text-sm text-gray-700 hover:bg-gray-50">
            <Settings2 className="h-4 w-4" aria-hidden="true" /> Columns &amp; WIP limits
          </button>
        )}
      </div>

      <div className="flex gap-4 overflow-x-auto pb-4">
        {board.columns.map((col) => {
          const cards = assignee ? col.items.filter((i) => String(i.assignedToId) === assignee) : col.items;
          const atLimit = col.wipLimit != null && col.count >= col.wipLimit;
          return (
            <section
              key={col.id}
              aria-label={`${col.name} column`}
              onDragOver={(e) => { if (drag && canEdit) { e.preventDefault(); setOverStatus(col.status); } }}
              onDragLeave={() => setOverStatus((s) => (s === col.status ? null : s))}
              onDrop={(e) => { e.preventDefault(); dropOn(col.status, null); }}
              className={`w-72 flex-shrink-0 rounded-xl bg-gray-100 border ${overStatus === col.status ? 'border-blue-400 ring-2 ring-blue-200' : 'border-transparent'}`}
            >
              <header className="flex items-center justify-between px-3 py-2.5">
                <h3 className="text-sm font-semibold text-gray-800">{col.name}</h3>
                <span className={`inline-flex items-center gap-1 text-xs font-semibold rounded-full px-2 py-0.5 ${col.overWip ? 'bg-red-100 text-red-700' : atLimit ? 'bg-amber-100 text-amber-700' : 'bg-white text-gray-600'}`}
                  title={col.wipLimit != null ? `WIP limit ${col.wipLimit}` : 'No WIP limit'}>
                  {(col.overWip || atLimit) && <AlertTriangle className="h-3 w-3" aria-hidden="true" />}
                  {col.count}{col.wipLimit != null && ` / ${col.wipLimit}`}
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
                      onDragOver={(e) => { if (drag && drag.id !== item.id && canEdit) e.preventDefault(); }}
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
                            {board.columns.map((c) => <option key={c.status} value={c.status}>{c.name}</option>)}
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

      {editing && (
        <WorkItemModal item={editing} items={allCards} members={members} canEdit={canEdit} onSave={saveItem} onClose={() => setEditing(null)} />
      )}
      {showSettings && (
        <BoardSettingsModal columns={board.columns} onSave={saveColumns} onClose={() => setShowSettings(false)} />
      )}
    </div>
  );
};

export default PmBoard;
