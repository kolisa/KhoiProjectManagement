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
  key === 'SuperAdmin' ? 'Super Admin' : PROJECT_ROLES.find((r) => r.key === key)?.label || key;

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

export const statusLabel = (key) => STATUSES.find((s) => s.key === key)?.label || key;

// Which parent types a child type may sit under (strictly higher level) - same rule as the backend.
const LEVEL = { Epic: 0, Feature: 1, Story: 2, Task: 3, Bug: 3, Subtask: 4 };
export const allowedParentTypes = (childType) =>
  Object.keys(LEVEL).filter((t) => LEVEL[t] < (LEVEL[childType] ?? -1));

// Mirrors ProjectRoles.Matrix on the server.
const CAN = {
  ProjectAdmin: ['create', 'edit', 'delete', 'rank', 'members', 'board'],
  ProjectManager: ['create', 'edit', 'delete', 'rank', 'board'],
  ScrumMaster: ['create', 'edit', 'delete', 'rank', 'board'],
  Developer: ['create', 'edit', 'rank'],
  Tester: ['create', 'edit'],
  Viewer: [],
};
export const can = (role, action) => role === 'SuperAdmin' || (CAN[role] || []).includes(action);
