import React, { useState, useEffect, useCallback } from 'react';
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
import { hasPermission } from '../../utils/permissions';
import { roleLabel } from './pmConstants';

const LAST_PROJECT_KEY = 'khoi_pm_project';

const readLastProject = () => {
  try { return Number(localStorage.getItem(LAST_PROJECT_KEY)) || null; } catch { return null; }
};

// The Project Management workspace: project switcher + Backlog / Members tabs (+ Access for
// pm.admin). Rendered inside the main app for users who also have other modules, and inside
// PmShell for module-only accounts - it only ever calls /api/pm/*, so it works for both.
const PmWorkspace = ({ apiService, user }) => {
  const [projects, setProjects] = useState([]);
  const [projectId, setProjectId] = useState(readLastProject);
  const [tab, setTab] = useState('overview');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const isPmAdmin = hasPermission(user?.permissions, 'pm.admin');

  const loadProjects = useCallback(async () => {
    try {
      setError(null);
      setProjects(await apiService.getPmProjects());
    } catch (err) {
      setError(err?.message || 'Could not load projects.');
    } finally {
      setLoading(false);
    }
  }, [apiService]);

  useEffect(() => { loadProjects(); }, [loadProjects]);

  const project = projects.find((p) => p.id === projectId) || projects[0] || null;

  const selectProject = (e) => {
    const id = Number(e.target.value);
    setProjectId(id);
    try { localStorage.setItem(LAST_PROJECT_KEY, String(id)); } catch { /* private mode - non-essential */ }
  };

  // "My work" lists items from every project; clicking one jumps to that project's backlog.
  const openProject = (id) => {
    setProjectId(id);
    try { localStorage.setItem(LAST_PROJECT_KEY, String(id)); } catch { /* private mode - non-essential */ }
    setTab('backlog');
  };

  const tabs = [
    { key: 'overview', label: 'Overview' },
    { key: 'mywork', label: 'My work' },
    { key: 'backlog', label: 'Backlog' },
    { key: 'sprints', label: 'Sprints' },
    { key: 'board', label: 'Board' },
    { key: 'reports', label: 'Reports' },
    { key: 'members', label: 'Members' },
    ...(isPmAdmin ? [{ key: 'access', label: 'Module access' }] : []),
  ];
  const activeTab = tabs.some((t) => t.key === tab) ? tab : 'overview';

  if (loading) return <LoadingSpinner />;
  if (error) return <ErrorMessage message={error} onRetry={loadProjects} />;

  return (
    <div>
      <div className="flex flex-wrap items-center gap-3 mb-5">
        <h1 className="text-2xl font-bold text-gray-900">Project Management</h1>
        {project && (
          <>
            <label htmlFor="pm-project" className="sr-only">Project</label>
            <select id="pm-project" value={project.id} onChange={selectProject}
              className="border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-blue-500">
              {projects.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}
            </select>
            <span className="text-xs text-gray-500">
              Your role: <strong className="text-gray-700">{roleLabel(project.myRole)}</strong> · {project.openItems} open of {project.totalItems} items
            </span>
          </>
        )}
      </div>

      <div role="tablist" aria-label="Project Management sections" className="flex gap-1 border-b border-gray-200 mb-5">
        {tabs.map((t) => (
          <button key={t.key} role="tab" aria-selected={activeTab === t.key} onClick={() => setTab(t.key)}
            className={`px-4 py-2 text-sm font-medium -mb-px border-b-2 ${activeTab === t.key ? 'border-blue-600 text-blue-700' : 'border-transparent text-gray-500 hover:text-gray-800'}`}>
            {t.label}
          </button>
        ))}
      </div>

      {activeTab === 'access' ? (
        <PmAccessAdmin apiService={apiService} />
      ) : activeTab === 'mywork' ? (
        <PmMyWork apiService={apiService} onOpenProject={openProject} />
      ) : !project ? (
        <div className="text-center py-16 text-gray-500 bg-white border border-dashed border-gray-300 rounded-xl">
          You are not a member of any project yet. Ask a Project Admin or SuperAdmin to add you.
        </div>
      ) : activeTab === 'overview' ? (
        <PmOverview key={project.id} apiService={apiService} project={project} />
      ) : activeTab === 'members' ? (
        <PmMembers key={project.id} apiService={apiService} project={project} onChanged={loadProjects}
          otherProjects={projects.filter((p) => p.id !== project.id)} canGrantAccess={isPmAdmin} />
      ) : (
        <PmWithMembers key={`${tab}-${project.id}`} apiService={apiService} project={project} view={activeTab} />
      )}
    </div>
  );
};

// The backlog and board both need the roster for their assignee pickers/filters; load it here so
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

export default PmWorkspace;
