// Driven by the backend-issued permission list (see AuthContext/ApiService), not a hardcoded map -
// `permissions` is the flat array of "resource.action" names returned by /api/auth/login and /api/auth/me.
export const hasPermission = (permissions, permissionName) => {
  return permissions?.includes(permissionName) ?? false;
};
