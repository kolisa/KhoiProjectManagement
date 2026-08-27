using KhoiProjectManagement.Domain;

namespace KhoiProjectManagement.Application
{
    public interface ISpacePermissionResolver
    {
        // Walks from spaceId upward through ParentSpaceId, returning the highest PermissionLevel
        // granted to userId, any of roleIds, or any of groupIds at the first Space with a matching
        // grant. Returns null (deny) if no grant is found before hitting an inheritance boundary or
        // the root.
        Task<PermissionLevel?> ResolveEffectiveLevelAsync(int spaceId, int userId, IEnumerable<int> roleIds, IEnumerable<int> groupIds);

        // Called by SpaceService after any write to Spaces/SpacePermissions so the next resolution
        // reflects the change instead of a stale cached snapshot.
        void InvalidateCache();
    }
}
