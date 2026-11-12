import React, { useEffect, useState, useMemo } from 'react';
import { ChevronRight, Users } from 'lucide-react';
import LoadingSpinner from '../Common/LoadingSpinner';
import ErrorMessage from '../Common/ErrorMessage';
import { TYPE_STYLES, effortLabel, fmtPoints, statusLabel } from './pmConstants';

// Points per PERSON for a sprint, drillable: person > item (and subtask) > category. Each person's total is the sum of what
// was allocated to them on the items in the sprint - never one number for the whole sprint or the whole item.
const CategoryChips = ({ cells }) => (
  <ul className="flex flex-wrap gap-1.5">
    {cells.map((c) => (
      <li key={c.category} className="rounded-full border border-gray-200 bg-white px-2 py-0.5 text-xs text-gray-700">
        {effortLabel(c.category)} <span className="font-semibold text-gray-900">{fmtPoints(c.points)}</span>
      </li>
    ))}
  </ul>
);

const ItemRow = ({ item }) => {
  const [open, setOpen] = useState(false);
  return (
    <li>
      <button type="button" aria-expanded={open} onClick={() => setOpen((o) => !o)}
        className="flex w-full items-center gap-2 px-4 py-2 pl-10 text-left text-sm hover:bg-gray-50">
        <ChevronRight className={`h-3.5 w-3.5 shrink-0 text-gray-400 transition-transform ${open ? 'rotate-90' : ''}`} aria-hidden="true" />
        <span className={`rounded-sm px-1.5 py-0.5 text-[10px] font-semibold ${TYPE_STYLES[item.type] || 'bg-gray-100 text-gray-700'}`}>{item.type}</span>
        <span className="min-w-0 flex-1 truncate text-gray-900">
          {item.title}{item.parentTitle && <span className="text-gray-500"> · in {item.parentTitle}</span>}
        </span>
        <span className="text-xs text-gray-500">{statusLabel(item.status)}</span>
        <span className="w-14 text-right font-semibold text-gray-900">{fmtPoints(item.total)}</span>
      </button>
      {open && <div className="bg-gray-50 px-4 py-2 pl-16"><CategoryChips cells={item.cells} /></div>}
    </li>
  );
};

const PersonRow = ({ person, max }) => {
  const [open, setOpen] = useState(false);
  const pct = max > 0 ? Math.round((person.total / max) * 100) : 0;
  const donePct = person.total > 0 ? Math.round((person.done / person.total) * 100) : 0;
  return (
    <li>
      <button type="button" aria-expanded={open} aria-label={`${person.userName}: ${fmtPoints(person.total)} points`} onClick={() => setOpen((o) => !o)}
        className="flex w-full items-center gap-3 px-4 py-3 text-left hover:bg-gray-50">
        <ChevronRight className={`h-4 w-4 shrink-0 text-gray-400 transition-transform ${open ? 'rotate-90' : ''}`} aria-hidden="true" />
        <span className="w-40 shrink-0 truncate text-sm font-medium text-gray-900">{person.userName}</span>
        <span className="relative h-2 flex-1 rounded-full bg-gray-100" aria-hidden="true">
          <span className="absolute inset-y-0 left-0 rounded-full bg-blue-200" style={{ width: `${pct}%` }} />
          <span className="absolute inset-y-0 left-0 rounded-full bg-blue-600" style={{ width: `${(pct * donePct) / 100}%` }} />
        </span>
        <span className="w-24 shrink-0 text-right text-sm text-gray-600">{fmtPoints(person.done)} done</span>
        <span className="w-16 shrink-0 text-right text-sm font-semibold text-gray-900">{fmtPoints(person.total)}</span>
      </button>
      {open && (
        <div className="border-t border-gray-100 bg-gray-50/60">
          <div className="px-4 py-2 pl-10">
            <p className="mb-1 text-[11px] font-medium uppercase tracking-wide text-gray-500">By category</p>
            <CategoryChips cells={person.categories} />
          </div>
          <p className="px-4 pt-1 pl-10 text-[11px] font-medium uppercase tracking-wide text-gray-500">By item</p>
          <ul className="divide-y divide-gray-100 pb-1">
            {person.items.map((i) => <ItemRow key={i.itemId} item={i} />)}
          </ul>
        </div>
      )}
    </li>
  );
};

const SprintEffort = ({ apiService, sprints, refreshKey }) => {
  const initial = useMemo(() => (sprints.find((s) => s.state === 'Active') || sprints.find((s) => s.state === 'Planned') || sprints[sprints.length - 1])?.id ?? '', [sprints]);
  const [sprintId, setSprintId] = useState(initial);
  const [data, setData] = useState(null);
  const [error, setError] = useState(null);

  useEffect(() => { if (sprintId === '' && initial !== '') setSprintId(initial); }, [initial, sprintId]);

  useEffect(() => {
    if (sprintId === '') return undefined;
    let live = true;
    setError(null);
    apiService.getSprintEffort(sprintId)
      .then((d) => { if (live) setData(d); })
      .catch((e) => { if (live) setError(e?.message || 'Could not load the points.'); });
    return () => { live = false; };
  }, [apiService, sprintId, refreshKey]);

  const max = data ? Math.max(0, ...data.people.map((p) => p.total)) : 0;

  return (
    <section aria-label="Points by person" className="bg-white border border-gray-200 rounded-xl">
      <header className="flex flex-wrap items-center gap-3 px-4 py-3 border-b border-gray-100">
        <h3 className="flex items-center gap-2 text-base font-semibold text-gray-900"><Users className="h-4 w-4 text-blue-600" aria-hidden="true" /> Points by person</h3>
        {data && <span className="text-sm text-gray-500">{fmtPoints(data.done)} of {fmtPoints(data.total)} done</span>}
        {sprints.length > 0 && (
          <>
            <label htmlFor="effort-sprint" className="sr-only">Sprint</label>
            <select id="effort-sprint" value={sprintId} onChange={(e) => { setData(null); setSprintId(Number(e.target.value)); }}
              className="ml-auto border border-gray-300 rounded-lg px-2 py-1 text-sm bg-white">
              {sprints.map((s) => <option key={s.id} value={s.id}>{s.name} ({s.state})</option>)}
            </select>
          </>
        )}
      </header>

      {sprints.length === 0 && <p className="text-sm text-gray-400 text-center py-6">Create a sprint to see points per person.</p>}
      {error && <div className="p-4"><ErrorMessage message={error} /></div>}
      {sprints.length > 0 && !error && !data && <LoadingSpinner text="Loading points..." />}

      {data && (
        <>
          {data.people.length === 0 ? (
            <p className="text-sm text-gray-400 text-center py-6">Nobody has been allocated points in this sprint yet. Open a work item and fill in its Effort.</p>
          ) : (
            <ul className="divide-y divide-gray-100">{data.people.map((p) => <PersonRow key={p.userId} person={p} max={max} />)}</ul>
          )}
          {data.unallocated.length > 0 && (
            <div role="note" className="border-t border-amber-200 bg-amber-50 px-4 py-3">
              <p className="text-xs font-medium text-amber-900">{data.unallocated.length} item{data.unallocated.length === 1 ? ' has' : 's have'} no effort allocated yet:</p>
              <p className="mt-1 text-xs text-amber-800">{data.unallocated.map((i) => i.title).join(' · ')}</p>
            </div>
          )}
        </>
      )}
    </section>
  );
};

export default SprintEffort;
