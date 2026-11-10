import React, { useState, useEffect, useCallback } from 'react';
import { ChevronRight, ChevronDown, Eye, Folder, LayoutDashboard, ListChecks, Zap, Kanban, BarChart3, Users } from 'lucide-react';
import LoadingSpinner from '../Common/LoadingSpinner';
import ErrorMessage from '../Common/ErrorMessage';
import PmBacklog from './PmBacklog';
import PmBoard from './PmBoard';
import PmSprints from './PmSprints';
import PmReports from './PmReports';
import PmOverview from './PmOverview';
import PmMyWork from './PmMyWork';
import PmMembers from './PmMembers';
import PmAccessAdmin from './PmAccessAdmin';
import PmProjectNav from './PmProjectNav';
import { Avatar, AccessBadge, RoleChip, EmptyState, cardClass } from './pmUi';
import { hasPermission } from '../../utils/permissions';
import { effectiveProject } from './pmConstants';

const LAST_PROJECT_KEY = 'khoi_pm_project';
const TAB_KEY_PREFIX = 'khoi_pm_tab_';

const TABS = [
  { key: 'overview', label: 'Overview', Icon: LayoutDashboard },
  { key: 'backlog', label: 'Backlog', Icon: ListChecks },
  { key: 'sprints', label: 'Sprints', Icon: Zap },
  { key: 'board', label: 'Board', Icon: Kanban },
  { key: 'reports', label: 'Reports', Icon: BarChart3 },
  { key: 'members', label: 'Members', Icon: Users },
];
const TAB_KEYS = TABS.map((t) => t.key);

// ---- where you are, in the address bar -----------------------------------------------------------------------
// #/pm/{projectId}/{tab}  |  #/pm/mywork  |  #/pm/access. The app has no router, so the workspace reads and writes
// this itself: a refresh, a bookmark, a shared link and the browser's back/forward buttons all land on the same
// project and tab.
const HASH_RE = /^#\/pm\/(?:(mywork|access)|(\d+)(?:\/([a-z]+))?)\/?$/;

export const parseHash = (hash) => {
  const m = HASH_RE.exec(hash || '');
  if (!m) return null;
  if (m[1]) return { kind: m[1] };
  return { kind: 'project', projectId: Number(m[2]), tab: TAB_KEYS.includes(m[3]) ? m[3] : null };
};

export const formatHash = (route) => (route.kind === 'project' ? `#/pm/${route.projectId}/${route.tab}` : `#/pm/${route.kind}`);

const safeGet = (key) => { try { return localStorage.getItem(key); } catch { return null; } };
const safeSet = (key, value) => { try { localStorage.setItem(key, value); } catch { /* private mode - non-essential */ } };

const storedTab = (projectId) => {
  const tab = safeGet(`${TAB_KEY_PREFIX}${projectId}`);
  return TAB_KEYS.includes(tab) ? tab : 'overview';
};

const initialRoute = () => {
  const fromHash = parseHash(window.location.hash);
  if (fromHash) return fromHash.kind === 'project' ? { ...fromHash, tab: fromHash.tab || storedTab(fromHash.projectId) } : fromHash;
  const last = Number(safeGet(LAST_PROJECT_KEY)) || null;
  return { kind: 'project', projectId: last, tab: last ? storedTab(last) : 'overview' };
};

// Backlog, board, sprints and reports all need the project roster for their assignee pickers; load it here so
// they stay pure consumers.
const PmWithMembers = ({ apiService, project, view }) => {
  const [members, setMembers] = useState(null);
  useEffect(() => {
    let cancelled = false;
    apiService.getPmMembers(project.id).then((m) => { if (!cancelled) setMembers(m); }).catch(() => { if (!cancelled) setMembers([]); });
    return () => { cancelled = true; };
  }, [apiService, project.id]);
  if (!members) return <LoadingSpinner />;
  const View = view === 'board' ? PmBoard : view === 'sprints' ? PmSprints : view === 'reports' ? PmReports : PmBacklog;
  return <View apiService={apiService} project={project} members={members} />;
};

// The Project Management workspace: a project/section list on the left, a header that always says which project and
// section you are in (and what you are allowed to do there), and that project's tabs. Rendered inside the main app
// for people with other modules, and inside PmShell for module-only accounts - it only ever calls /api/pm/*.
const PmWorkspace = ({ apiService, user }) => {
  const [projects, setProjects] = useState(null);
  const [error, setError] = useState(null);
  const [route, setRoute] = useState(initialRoute);
  const [navOpen, setNavOpen] = useState(false);
  // Whether this person administers the module. The server decides (a Project Admin counts as an administrator, and that
  // is a database fact the JWT cannot carry); the pm.admin permission is only the answer until the server has replied.
  const [serverAdmin, setServerAdmin] = useState(null);
  const isPmAdmin = serverAdmin ?? hasPermission(user?.permissions, 'pm.admin');

  const loadProjects = useCallback(async () => {
    try {
      setError(null);
      const [list, capabilities] = await Promise.all([
        apiService.getPmProjects(),
        apiService.getPmCapabilities ? apiService.getPmCapabilities().catch(() => null) : Promise.resolve(null),
      ]);
      setServerAdmin(capabilities ? Boolean(capabilities.isAdmin) : null);
      setProjects(list);
    } catch (err) {
      setError(err?.message || 'Could not load projects.');
    }
  }, [apiService]);

  useEffect(() => { loadProjects(); }, [loadProjects]);

  const navigate = useCallback((next, { replace = false } = {}) => {
    setRoute(next);
    setNavOpen(false);
    const hash = formatHash(next);
    try {
      if (window.location.hash !== hash) window.history[replace ? 'replaceState' : 'pushState'](null, '', hash);
    } catch { /* an embedded/locked-down browser - the in-memory route still works */ }
    if (next.kind === 'project') {
      safeSet(LAST_PROJECT_KEY, String(next.projectId));
      safeSet(`${TAB_KEY_PREFIX}${next.projectId}`, next.tab);
    }
  }, []);

  // Back / forward / a hand-edited address.
  useEffect(() => {
    const onNavigate = () => {
      const parsed = parseHash(window.location.hash);
      if (!parsed) return;
      setRoute(parsed.kind === 'project' ? { ...parsed, tab: parsed.tab || storedTab(parsed.projectId) } : parsed);
    };
    window.addEventListener('popstate', onNavigate);
    window.addEventListener('hashchange', onNavigate);
    return () => {
      window.removeEventListener('popstate', onNavigate);
      window.removeEventListener('hashchange', onNavigate);
    };
  }, []);

  // Once the project list is known, make sure the route points somewhere real the person may be.
  useEffect(() => {
    if (!projects) return;
    if (route.kind === 'access' && !isPmAdmin) {
      if (projects[0]) navigate({ kind: 'project', projectId: projects[0].id, tab: storedTab(projects[0].id) }, { replace: true });
      return;
    }
    if (route.kind !== 'project') return;
    const known = projects.some((p) => p.id === route.projectId);
    if (!known) {
      if (projects[0]) navigate({ kind: 'project', projectId: projects[0].id, tab: route.tab || 'overview' }, { replace: true });
    } else if (window.location.hash !== formatHash(route)) {
      navigate(route, { replace: true });   // put the current place into the address bar
    }
  }, [projects, route, isPmAdmin, navigate]);

  const project = route.kind === 'project' && projects ? projects.find((p) => p.id === route.projectId) : null;
  const tab = route.kind === 'project' ? route.tab || 'overview' : null;
  const tabInfo = TABS.find((t) => t.key === tab);

  // The browser tab always names the place.
  useEffect(() => {
    const previous = document.title;
    if (route.kind === 'project' && project) document.title = `${project.name} · ${tabInfo?.label || ''} · Project Management`;
    else if (route.kind === 'mywork') document.title = 'My work · Project Management';
    else if (route.kind === 'access') document.title = 'Module access · Project Management';
    return () => { document.title = previous; };
  }, [route, project, tabInfo]);

  const openProject = (id) => {
    // Keep the section you are on when you switch project - Board of A -> Board of B - so you always know where you are.
    const keepTab = route.kind === 'project' ? route.tab : null;
    navigate({ kind: 'project', projectId: id, tab: keepTab || storedTab(id) });
  };
  const openSection = (kind) => navigate({ kind });
  const openTab = (key) => navigate({ kind: 'project', projectId: route.projectId, tab: key });

  if (error) return <ErrorMessage message={error} onRetry={loadProjects} />;
  if (!projects) return <LoadingSpinner />;

  const effective = effectiveProject(project);
  const sectionTitle = route.kind === 'mywork' ? 'My work' : route.kind === 'access' ? 'Module access' : null;

  return (
    <div className="lg:grid lg:grid-cols-[15rem_minmax(0,1fr)] lg:items-start lg:gap-6">
      {/* Small screens: one button that names the current place and opens the same list. */}
      <div className="mb-3 lg:hidden">
        <button onClick={() => setNavOpen((o) => !o)} aria-expanded={navOpen} aria-controls="pm-nav"
          className={`${cardClass} flex w-full items-center gap-3 px-4 py-3 text-left`}>
          <Folder className="h-5 w-5 text-blue-600" aria-hidden="true" />
          <span className="min-w-0 flex-1">
            <span className="block text-[11px] font-semibold uppercase tracking-wide text-gray-400">You are in</span>
            <span className="block truncate text-sm font-semibold text-gray-900">{project ? project.name : sectionTitle || 'Project Management'}</span>
          </span>
          <ChevronDown className={`h-4 w-4 text-gray-500 transition-transform ${navOpen ? 'rotate-180' : ''}`} aria-hidden="true" />
        </button>
      </div>

      <aside id="pm-nav" className={`${navOpen ? 'block' : 'hidden'} mb-4 lg:sticky lg:top-4 lg:mb-0 lg:block`}>
        <div className={`${cardClass} p-3`}>
          <PmProjectNav projects={projects} route={route} canAdminAccess={isPmAdmin} onOpenProject={openProject} onOpenSection={openSection} />
        </div>
      </aside>

      <section aria-label="Project Management content" className="min-w-0">
        {route.kind === 'mywork' && (
          <>
            <Header crumbs={['Project Management', 'My work']} title="My work" subtitle="Everything assigned to you, across every project." />
            <PmMyWork apiService={apiService} onOpenProject={(id) => navigate({ kind: 'project', projectId: id, tab: 'backlog' })} />
          </>
        )}

        {route.kind === 'access' && (
          <>
            <Header crumbs={['Project Management', 'Module access']} title="Module access"
              subtitle="Who can use Project Management, and whether they can read only or read and write." />
            <PmAccessAdmin apiService={apiService} />
          </>
        )}

        {route.kind === 'project' && !project && (
          <EmptyState icon={Folder} title="You are not on any project yet">
            Ask a Project Admin to add you to a project{isPmAdmin ? ', or open Module access to review who can use this module' : ''}.
          </EmptyState>
        )}

        {route.kind === 'project' && project && (
          <>
            <Header crumbs={['Project Management', project.name, tabInfo?.label]}
              title={project.name}
              avatar={<Avatar name={project.name} size="lg" square />}
              badges={(
                <>
                  <RoleChip role={project.myRole} />
                  <AccessBadge role={project.myRole} readOnly={project.readOnly} />
                  <span className="text-xs text-gray-500">{project.openItems} open of {project.totalItems} items</span>
                </>
              )}
            />

            {project.readOnly && (
              <div role="note" className="mb-4 flex items-start gap-2 rounded-lg border border-gray-200 bg-gray-50 px-4 py-3 text-sm text-gray-700">
                <Eye className="mt-0.5 h-4 w-4 flex-shrink-0 text-gray-500" aria-hidden="true" />
                <span><strong>Read-only access.</strong> You can see everything in this project but not change it. Ask an administrator if you need read and write access.</span>
              </div>
            )}

            <div role="tablist" aria-label={`${project.name} sections`} className="mb-5 flex gap-1 overflow-x-auto border-b border-gray-200">
              {TABS.map(({ key, label, Icon }) => (
                <button key={key} role="tab" aria-selected={tab === key} onClick={() => openTab(key)}
                  className={`-mb-px inline-flex items-center gap-1.5 whitespace-nowrap border-b-2 px-3.5 py-2.5 text-sm font-medium transition-colors ${tab === key ? 'border-blue-600 text-blue-700' : 'border-transparent text-gray-500 hover:text-gray-800'}`}>
                  <Icon className="h-4 w-4" aria-hidden="true" /> {label}
                </button>
              ))}
            </div>

            {tab === 'overview' && <PmOverview key={project.id} apiService={apiService} project={effective} />}
            {tab === 'members' && (
              <PmMembers key={project.id} apiService={apiService} project={effective} onChanged={loadProjects}
                otherProjects={projects.filter((p) => p.id !== project.id)} canGrantAccess={isPmAdmin} currentUserId={user?.id ?? null} />
            )}
            {tab !== 'overview' && tab !== 'members' && (
              <PmWithMembers key={`${tab}-${project.id}`} apiService={apiService} project={effective} view={tab} />
            )}
          </>
        )}
      </section>
    </div>
  );
};

// Breadcrumb + title + badges. The breadcrumb is the "you are here" line: Project Management > Project > Section.
const Header = ({ crumbs, title, subtitle, avatar, badges }) => (
  <header className={`${cardClass} mb-4 p-5`}>
    <nav aria-label="Breadcrumb">
      <ol className="flex flex-wrap items-center gap-1 text-xs text-gray-500">
        {crumbs.filter(Boolean).map((c, i, all) => (
          <li key={`${c}-${i}`} className="flex items-center gap-1">
            {i > 0 && <ChevronRight className="h-3 w-3 text-gray-300" aria-hidden="true" />}
            <span aria-current={i === all.length - 1 ? 'page' : undefined} className={i === all.length - 1 ? 'font-medium text-gray-700' : ''}>{c}</span>
          </li>
        ))}
      </ol>
    </nav>
    <div className="mt-3 flex items-center gap-4">
      {avatar}
      <div className="min-w-0">
        <h1 className="truncate text-xl font-bold text-gray-900">{title}</h1>
        {subtitle && <p className="text-sm text-gray-500">{subtitle}</p>}
        {badges && <div className="mt-1.5 flex flex-wrap items-center gap-2">{badges}</div>}
      </div>
    </div>
  </header>
);

export default PmWorkspace;
