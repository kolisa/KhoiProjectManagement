// Shared vocabulary for the Project Management module - mirrors the backend's ProjectRoles /
// WorkItemHierarchy (Application/ProjectManagement/ProjectRoles.cs). The server is the authority;
// these only drive labels, pickers and which buttons are shown.

export const PROJECT_ROLES = [
  { key: 'ProjectAdmin', label: 'Project Admin' },
  { key: 'ProjectManager', label: 'Project Manager' },
  { key: 'ScrumMaster', label: 'Scrum Master' },
  { key: 'Developer', label: 'Developer' },
  { key: 'Tester', label: 'Tester' },
  { key: 'Viewer', label: 'Viewer' },
];

export const roleLabel = (key) =>
  key === 'SuperAdmin' ? 'Administrator' : PROJECT_ROLES.find((r) => r.key === key)?.label || key;

export const WORK_ITEM_TYPES = ['Epic', 'Feature', 'Story', 'Task', 'Bug', 'Subtask'];

export const TYPE_STYLES = {
  Epic: 'bg-purple-100 text-purple-700',
  Feature: 'bg-indigo-100 text-indigo-700',
  Story: 'bg-green-100 text-green-700',
  Task: 'bg-blue-100 text-blue-700',
  Bug: 'bg-red-100 text-red-700',
  Subtask: 'bg-gray-100 text-gray-700',
};

export const STATUSES = [
  { key: 'todo', label: 'To do' },
  { key: 'in-progress', label: 'In progress' },
  { key: 'blocked', label: 'Blocked' },
  { key: 'completed', label: 'Done' },
];

export const STATUS_STYLES = {
  todo: 'bg-gray-100 text-gray-700',
  'in-progress': 'bg-blue-100 text-blue-700',
  blocked: 'bg-red-100 text-red-700',
  completed: 'bg-green-100 text-green-700',
};

// The dot beside a status wherever a column or group is headed by it.
export const STATUS_DOT = { todo: 'bg-gray-400', 'in-progress': 'bg-blue-500', blocked: 'bg-red-500', completed: 'bg-green-500' };

export const statusLabel = (key) => STATUSES.find((s) => s.key === key)?.label || key;

// Which parent types a child type may sit under (strictly higher level) - same rule as the backend.
const LEVEL = { Epic: 0, Feature: 1, Story: 2, Task: 3, Bug: 3, Subtask: 4 };
export const allowedParentTypes = (childType) =>
  Object.keys(LEVEL).filter((t) => LEVEL[t] < (LEVEL[childType] ?? -1));

// Mirrors ProjectRoles.Matrix on the server.
const CAN = {
  ProjectAdmin: ['create', 'edit', 'delete', 'rank', 'members', 'board', 'sprint', 'collaborate'],
  ProjectManager: ['create', 'edit', 'delete', 'rank', 'board', 'sprint', 'collaborate'],
  ScrumMaster: ['create', 'edit', 'delete', 'rank', 'board', 'sprint', 'collaborate'],
  Developer: ['create', 'edit', 'rank', 'collaborate'],
  Tester: ['create', 'edit', 'collaborate'],
  Viewer: [],
};
export const can = (role, action) => role === 'SuperAdmin' || (CAN[role] || []).includes(action);

// ---- Access levels ("policies") ------------------------------------------------------------------------
// A person's access in a project is the combination of two things: their per-project ROLE, and their module-wide
// access LEVEL (read only vs read & write, set by an administrator). Read-only wins over any role. These helpers
// describe the result in one word; the server enforces it.
export const ACCESS_LEVEL_LABELS = { read: 'Read only', write: 'Read & write', full: 'Full control' };

export const accessLevel = (role, readOnly = false) => {
  if (role === 'SuperAdmin') return 'full';
  if (readOnly || role === 'Viewer' || !role) return 'read';
  return role === 'ProjectAdmin' ? 'full' : 'write';
};

// What each ability means in plain words, in the order the roles table lists them. `key` is a can() action.
export const ABILITY_ROWS = [
  { key: 'view', label: 'See the backlog, board, sprints and reports' },
  { key: 'create', label: 'Create work items' },
  { key: 'edit', label: 'Edit work items, checklists and links' },
  { key: 'collaborate', label: 'Comment and attach files' },
  { key: 'rank', label: 'Re-order the backlog' },
  { key: 'delete', label: 'Delete work items' },
  { key: 'board', label: 'Configure board columns, WIP limits and workflow' },
  { key: 'sprint', label: 'Plan, start and complete sprints' },
  { key: 'members', label: 'Manage members and roles' },
];

export const roleCan = (role, key) => key === 'view' || can(role, key);

// The project as the screens should treat it: a person whose module access is read-only gets the Viewer
// abilities everywhere, while `actualRole` keeps the real role for display.
export const effectiveProject = (project) => (project
  ? { ...project, actualRole: project.myRole, myRole: project.readOnly ? 'Viewer' : project.myRole }
  : project);
