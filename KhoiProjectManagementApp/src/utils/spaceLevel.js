// Mirrors the backend's PermissionLevel ordinal (Read < Write < Manage) - see
// KhoiProjectManagement.Models/SpacePermission.cs. Used to decide which affordances a Space's
// MyEffectiveLevel (a string: "Read"/"Write"/"Manage"/null) unlocks in the Vault/Wiki UI.
const LEVEL_RANK = { Read: 1, Write: 2, Manage: 3 };

export const hasSpaceLevel = (myEffectiveLevel, requiredLevel) => {
  if (!myEffectiveLevel) return false;
  return (LEVEL_RANK[myEffectiveLevel] || 0) >= (LEVEL_RANK[requiredLevel] || 0);
};
