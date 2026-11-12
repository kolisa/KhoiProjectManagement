import React, { useState } from 'react';
import { Search, ListTodo, ShieldCheck, Eye } from 'lucide-react';
import { Avatar } from './pmUi';
import { roleLabel } from './pmConstants';

const itemBase = 'group flex w-full items-center gap-3 rounded-lg px-2.5 py-2 text-left text-sm transition-colors focus:outline-hidden focus-visible:ring-2 focus-visible:ring-blue-500';
const itemIdle = 'text-gray-700 hover:bg-gray-100';
const itemActive = 'bg-blue-50 text-blue-800 font-medium';

const GroupLabel = ({ children }) => (
  <p className="px-2.5 pb-1 pt-3 text-[11px] font-semibold uppercase tracking-wide text-gray-400 first:pt-0">{children}</p>
);

// The "where am I / where can I go" list: workspace sections (My work, Module access) and every project the person
// belongs to. The current place is highlighted and marked aria-current, so it is obvious - and screen-reader
// announced - at all times, whichever project or section you are in.
const PmProjectNav = ({ projects, route, canAdminAccess, onOpenProject, onOpenSection }) => {
  const [query, setQuery] = useState('');
  const showSearch = projects.length > 7;
  const shown = query.trim()
    ? projects.filter((p) => p.name.toLowerCase().includes(query.trim().toLowerCase()))
    : projects;

  return (
    <nav aria-label="Projects and sections">
      <GroupLabel>Workspace</GroupLabel>
      <ul className="space-y-0.5">
        <li>
          <button onClick={() => onOpenSection('mywork')} aria-current={route.kind === 'mywork' ? 'page' : undefined}
            className={`${itemBase} ${route.kind === 'mywork' ? itemActive : itemIdle}`}>
            <ListTodo className="h-4 w-4 text-gray-500" aria-hidden="true" /> My work
          </button>
        </li>
        {canAdminAccess && (
          <li>
            <button onClick={() => onOpenSection('access')} aria-current={route.kind === 'access' ? 'page' : undefined}
              className={`${itemBase} ${route.kind === 'access' ? itemActive : itemIdle}`}>
              <ShieldCheck className="h-4 w-4 text-gray-500" aria-hidden="true" /> Module access
            </button>
          </li>
        )}
      </ul>

      <GroupLabel>Projects ({projects.length})</GroupLabel>
      {showSearch && (
        <div className="relative mb-1.5 px-0.5">
          <Search className="pointer-events-none absolute left-3 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-gray-400" aria-hidden="true" />
          <input aria-label="Find a project" placeholder="Find a project" value={query} onChange={(e) => setQuery(e.target.value)}
            className="w-full rounded-lg border border-gray-300 py-1.5 pl-8 pr-2 text-sm focus:outline-hidden focus:ring-2 focus:ring-blue-500" />
        </div>
      )}
      {shown.length === 0 ? (
        <p className="px-2.5 py-2 text-sm text-gray-500">{projects.length === 0 ? 'No projects yet.' : 'No project matches.'}</p>
      ) : (
        <ul className="space-y-0.5">
          {shown.map((p) => {
            const active = route.kind === 'project' && route.projectId === p.id;
            return (
              <li key={p.id}>
                <button onClick={() => onOpenProject(p.id)} aria-current={active ? 'page' : undefined}
                  className={`${itemBase} ${active ? itemActive : itemIdle}`}>
                  <Avatar name={p.name} size="sm" square />
                  <span className="min-w-0 flex-1">
                    <span className="block truncate">{p.name}</span>
                    <span className="flex items-center gap-1 truncate text-[11px] font-normal text-gray-500">
                      {p.readOnly && <Eye className="h-3 w-3" aria-label="Read only" />}
                      {roleLabel(p.myRole)} · {p.openItems} open
                    </span>
                  </span>
                </button>
              </li>
            );
          })}
        </ul>
      )}
    </nav>
  );
};

export default PmProjectNav;
