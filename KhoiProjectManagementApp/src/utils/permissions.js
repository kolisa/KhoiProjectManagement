export const hasPermission = (userRole, action) => {
  const permissions = {
    admin: ['create', 'edit', 'delete', 'assign', 'reports', 'manage_users'],
    manager: ['create', 'edit', 'assign', 'reports'],
    member: ['create', 'edit']
  };
  return permissions[userRole]?.includes(action) || false;
};