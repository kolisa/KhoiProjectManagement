import React, { useState, useEffect, useMemo, useCallback } from 'react';
import { Plus, Search, ArrowUp, ArrowDown, GripVertical, Trash2 } from 'lucide-react';
import LoadingSpinner from '../Common/LoadingSpinner';
import ErrorMessage from '../Common/ErrorMessage';
import PriorityBadge from '../Common/PriorityBadge';
import StatusBadge from '../Common/StatusBadge';
import WorkItemModal from './WorkItemModal';
import { LabelChips, RollupBar } from './pmWidgets';
import { EmptyState } from './pmUi';
import Combobox from './Combobox';
import { useToast } from '../../contexts/ToastContext';
import { useConfirm } from '../../contexts/ConfirmContext';
import { reportApiError } from '../../utils/apiError';
import { WORK_ITEM_TYPES, TYPE_STYLES, STATUSES, STATUS_STYLES, statusLabel, can, denyReason } from './pmConstants';

const selectClass = 'border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-blue-500';

// Ranked backlog: the project's work items in priority order, with filters, create/edit, and
// reordering by drag-and-drop (native HTML5 - no extra dependency) or the keyboard-accessible
// up/down buttons. Reordering is only offered on the unfiltered list, where "position" means something.
const PmBacklog = ({ apiService, project, members }) => {
  const toast = useToast();
  const confirm = useConfirm();
  const [items, setItems] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [filters, setFilters] = useState({ search: '', type: '', status: '', assigneeId: '', labelId: '' });
  const [labels, setLabels] = useState([]);
  const [editing, setEditing] = useState(undefined); // undefined = closed, null = new, object = edit
  const [dragId, setDragId] = useState(null);

  const role = project.myRole;
  const canCreate = can(role, 'create');
  const canEdit = can(role, 'edit');
  const canDelete = can(role, 'delete');
  const canRank = can(role, 'rank');
  const [sprints, setSprints] = useState([]);
  const canPlan = can(project.myRole, 'sprint');
  const planReason = denyReason(project, 'sprint');
  const createReason = denyReason(project, 'create');
  const deleteReason = denyReason(project, 'delete');
  const rankReason = denyReason(project, 'rank');

  useEffect(() => {
    apiService.getSprints(project.id).then((all) => setSprints(all.filter((x) => x.state !== 'Completed'))).catch(() => setSprints([]));
  }, [apiService, project.id]);

  const assignSprint = async (item, sprintId) => {
    try {
      await apiService.assignWorkItemSprint(item.id, sprintId);
      toast.success(sprintId == null ? 'Moved back to the backlog.' : `Added to ${sprints.find((x) => x.id === sprintId)?.name || 'the sprint'}.`);
      await load();
    } catch (err) { reportApiError(toast, err, 'Could not change the sprint.'); }
  };
  const isFiltered = Object.values(filters).some(Boolean);

  const load = useCallback(async () => {
    try {
      setError(null);
      setItems(await apiService.getWorkItems(project.id, filters));
    } catch (err) {
      setError(err?.message || 'Could not load the backlog.');
    } finally {
      setLoading(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [apiService, project.id, filters]);

  useEffect(() => {
    setLoading(true);
    const handle = setTimeout(load, filters.search ? 250 : 0); // debounce typing only
    return () => clearTimeout(handle);
  }, [load, filters.search]);

  const loadLabels = useCallback(() => {
    apiService.getProjectLabels(project.id).then(setLabels).catch(() => setLabels([]));
  }, [apiService, project.id]);

  useEffect(() => { loadLabels(); }, [loadLabels]);

  const totalPoints = useMemo(() => items.reduce((sum, i) => sum + (i.storyPoints || 0), 0), [items]);

  const save = async (dto) => {
    if (editing) await apiService.updateWorkItem(editing.id, dto);
    else await apiService.createWorkItem(project.id, dto);
    toast.success(editing ? 'Work item updated.' : 'Work item created.');
    setEditing(undefined);
    await load();
  };

  const remove = async (item) => {
    const ok = await confirm(`Delete "${item.title}"? This cannot be undone.`, { title: 'Delete work item', confirmText: 'Delete', danger: true });
    if (!ok) return;
    try {
      await apiService.deleteWorkItem(item.id);
      toast.success('Work item deleted.');
      await load();
    } catch (err) {
      reportApiError(toast, err, 'Could not delete the work item.');
    }
  };

  // Moves `itemId` so it lands at `targetIndex` of the list without it; the server takes "place after X".
  const moveTo = async (itemId, targetIndex) => {
    const without = items.filter((i) => i.id !== itemId);
    const moved = items.find((i) => i.id === itemId);
    const clamped = Math.max(0, Math.min(targetIndex, without.length));
    const afterId = clamped === 0 ? null : without[clamped - 1].id;
    const next = [...without.slice(0, clamped), moved, ...without.slice(clamped)];
    if (next.every((i, idx) => i.id === items[idx].id)) return;
    setItems(next); // optimistic
    try {
      await apiService.rankWorkItem(itemId, afterId);
    } catch (err) {
      reportApiError(toast, err, 'Could not reorder the backlog.');
      await load();
    }
  };

  const setFilter = (key) => (e) => setFilters((f) => ({ ...f, [key]: e.target.value }));

  return (
    <div>
      <div className="flex flex-wrap items-center gap-2 mb-4">
        <div className="relative flex-1 min-w-[200px]">
          <Search className="h-4 w-4 text-gray-400 absolute left-3 top-1/2 -translate-y-1/2" aria-hidden="true" />
          <input
            aria-label="Search backlog"
            placeholder="Search title or description"
            value={filters.search}
            onChange={setFilter('search')}
            className={`${selectClass} w-full pl-9`}
          />
        </div>
        <select aria-label="Filter by type" className={selectClass} value={filters.type} onChange={setFilter('type')}>
          <option value="">All types</option>
          {WORK_ITEM_TYPES.map((t) => <option key={t}>{t}</option>)}
        </select>
        <select aria-label="Filter by status" className={selectClass} value={filters.status} onChange={setFilter('status')}>
          <option value="">All statuses</option>
          {STATUSES.map((s) => <option key={s.key} value={s.key}>{s.label}</option>)}
        </select>
        <select aria-label="Filter by assignee" className={selectClass} value={filters.assigneeId} onChange={setFilter('assigneeId')}>
          <option value="">Everyone</option>
          {members.map((m) => <option key={m.userId} value={m.userId}>{m.name}</option>)}
        </select>
        {labels.length > 0 && (
          <select aria-label="Filter by label" className={selectClass} value={filters.labelId} onChange={setFilter('labelId')}>
            <option value="">All labels</option>
            {labels.map((l) => <option key={l.id} value={l.id}>{l.name}</option>)}
          </select>
        )}
        <button onClick={() => setEditing(null)} disabled={!canCreate} title={createReason || undefined}
          className="inline-flex items-center gap-1.5 px-4 py-2 rounded-lg bg-blue-600 text-white text-sm font-medium hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed">
          <Plus className="h-4 w-4" aria-hidden="true" /> New item
        </button>
      </div>

      <p className="text-xs text-gray-500 mb-2" aria-live="polite">
        {items.length} item{items.length === 1 ? '' : 's'} · {totalPoints} story points
        {isFiltered && canRank && ' · clear filters to reorder'}
      </p>

      {loading ? (
        <LoadingSpinner />
      ) : error ? (
        <ErrorMessage message={error} onRetry={load} />
      ) : items.length === 0 ? (
        <EmptyState icon={isFiltered ? Search : Plus} title={isFiltered ? 'No work items match these filters' : 'The backlog is empty'}>
          {isFiltered ? 'Try clearing a filter or searching for something else.' : canCreate ? 'Create an Epic or a Story to get started.' : 'Nothing has been added to this project yet.'}
        </EmptyState>
      ) : (
        <ul className="bg-white border border-gray-200 rounded-xl divide-y divide-gray-100 overflow-hidden">
          {items.map((item, index) => (
            <li
              key={item.id}
              draggable={canRank && !isFiltered}
              onDragStart={() => setDragId(item.id)}
              onDragEnd={() => setDragId(null)}
              onDragOver={(e) => { if (dragId && dragId !== item.id) e.preventDefault(); }}
              onDrop={(e) => { e.preventDefault(); if (dragId && dragId !== item.id) moveTo(dragId, index); setDragId(null); }}
              className={`flex items-center gap-3 px-3 py-2.5 hover:bg-gray-50 ${dragId === item.id ? 'opacity-40' : ''}`}
            >
              {!isFiltered && (
                <div className={`flex items-center ${canRank ? 'text-gray-400' : 'text-gray-300'}`} title={rankReason || undefined}>
                  <GripVertical className={`h-4 w-4 ${canRank ? 'cursor-grab' : 'cursor-not-allowed'}`} aria-hidden="true" />
                  <div className="flex flex-col">
                    <button aria-label={`Move ${item.title} up`} disabled={!canRank || index === 0} onClick={() => moveTo(item.id, index - 1)} className="hover:text-blue-600 disabled:opacity-30 disabled:cursor-not-allowed"><ArrowUp className="h-3.5 w-3.5" /></button>
                    <button aria-label={`Move ${item.title} down`} disabled={!canRank || index === items.length - 1} onClick={() => moveTo(item.id, index + 1)} className="hover:text-blue-600 disabled:opacity-30 disabled:cursor-not-allowed"><ArrowDown className="h-3.5 w-3.5" /></button>
                  </div>
                </div>
              )}

              <span className={`px-2 py-0.5 rounded text-[11px] font-semibold ${TYPE_STYLES[item.type] || 'bg-gray-100 text-gray-700'}`}>{item.type}</span>

              <button onClick={() => setEditing(item)} className="flex-1 min-w-0 text-left">
                <span className="block text-sm font-medium text-gray-900 truncate">{item.title}</span>
                <span className="block text-xs text-gray-500 truncate">
                  #{item.id}
                  {item.parentTitle && ` · in ${item.parentTitle}`}
                  {item.sprintName && ` · ${item.sprintName}`}
                  {item.childCount > 0 && ` · ${item.childCount} child item${item.childCount === 1 ? '' : 's'}`}
                  {item.isOverdue && <span className="text-red-600 font-medium"> · overdue</span>}
                </span>
                {(item.rollupTotal > 0 || item.labels?.length > 0) && (
                  <span className="mt-1 flex flex-wrap items-center gap-2">
                    <RollupBar done={item.rollupDone} total={item.rollupTotal} />
                    <LabelChips labels={item.labels} />
                  </span>
                )}
              </button>

              {!['Epic', 'Feature'].includes(item.type) && (
                <div className="hidden md:block w-36">
                  <label htmlFor={`sprint-${item.id}`} className="sr-only">Sprint for {item.title}</label>
                  <Combobox id={`sprint-${item.id}`} size="sm" value={item.sprintId ?? ''} disabled={!canPlan} title={planReason || undefined}
                    onChange={(v) => assignSprint(item, v === '' ? null : Number(v))} emptyLabel="No sprint" placeholder="Add to sprint…" searchPlaceholder="Search sprints…"
                    options={[...sprints.map((sp) => ({ value: sp.id, label: sp.name, description: sp.state === 'Active' ? 'Active' : 'Planned' })),
                      ...(item.sprintId != null && !sprints.some((sp) => sp.id === item.sprintId) ? [{ value: item.sprintId, label: item.sprintName || 'Closed sprint', description: 'Completed' }] : [])]} />
                </div>
              )}
              <span className="hidden md:inline text-xs text-gray-500 w-28 truncate">{item.assignedToName || 'Unassigned'}</span>
              {item.storyPoints != null && (
                <span className="text-xs font-semibold bg-gray-100 text-gray-700 rounded-full px-2 py-0.5" title="Story points">{item.storyPoints}</span>
              )}
              <PriorityBadge priority={item.priority} />
              <StatusBadge status={item.status} label={statusLabel(item.status)} colorMap={STATUS_STYLES} />

              <button aria-label={`Delete ${item.title}`} onClick={() => remove(item)} disabled={!canDelete} title={deleteReason || undefined}
                className="text-gray-400 hover:text-red-600 disabled:text-gray-300 disabled:hover:text-gray-300 disabled:cursor-not-allowed">
                <Trash2 className="h-4 w-4" />
              </button>
            </li>
          ))}
        </ul>
      )}

      {editing !== undefined && (
        <WorkItemModal
          item={editing}
          items={items}
          members={members}
          apiService={apiService}
          project={project}
          role={project.myRole}
          onChanged={() => { load(); loadLabels(); }}
          canEdit={editing ? canEdit : canCreate}
          onSave={save}
          onClose={() => setEditing(undefined)}
        />
      )}
    </div>
  );
};

export default PmBacklog;
