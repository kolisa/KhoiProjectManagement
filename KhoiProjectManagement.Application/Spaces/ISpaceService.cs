using System.Security.Claims;
using KhoiProjectManagement.Domain;
using KhoiProjectManagement.Application;

namespace KhoiProjectManagement.Application
{
    public interface ISpaceService
    {
        // Returns the project's existing home Space, or lazily creates one (nested under a shared
        // "Projects" root Space) the first time it's needed - a project with no synced team members
        // never gets a Space row at all.
        Task<int> EnsureProjectSpaceAsync(int projectId, int createdByUserId);

        // Fully reconciles a Space's user-targeted grants to exactly match userIds (adds missing,
        // removes stale) - the same full-reconciliation pattern ProjectService already uses for
        // ProjectUsers via AddRange/RemoveRange.
        Task SyncSpaceMembersAsync(int spaceId, IEnumerable<int> userIds, PermissionLevel level, int createdByUserId);

        // Generic Space CRUD - reused by vault category management and every future Space-scoped
        // module, not a vault-specific API. Both list/get methods filter to Spaces the caller has at
        // least Read on (and populate SpaceDto.MyEffectiveLevel) - listing must not leak the
        // existence/names of Spaces the caller can't access.
        Task<List<SpaceDto>> GetSpacesAsync(int? parentSpaceId, ClaimsPrincipal caller);
        Task<SpaceDto?> GetSpaceByIdAsync(int id, ClaimsPrincipal caller);
        Task<SpaceDto> CreateSpaceAsync(CreateSpaceDto dto, int createdByUserId);
        Task<bool> UpdateSpaceAsync(int id, UpdateSpaceDto dto);
        Task<bool> DeleteSpaceAsync(int id);

        Task<List<SpacePermissionDto>> GetSpacePermissionsAsync(int spaceId);

        // Distinct people who can access this Space - direct user grants plus everyone covered by a
        // role grant, deduplicated. Safe to expose to any caller who can already see the Space (unlike
        // GetSpacePermissionsAsync, a plain count reveals nothing about who specifically has access).
        Task<int> GetSpaceGranteeCountAsync(int spaceId);

        // Full replace of a Space's grants (both role- and user-targeted) - PUT semantics. Always
        // preserves the calling user's own Manage grant regardless of what's submitted, so a caller
        // can never accidentally lock themselves (or everyone) out of a Space they can currently manage.
        Task<bool> SetSpacePermissionsAsync(int spaceId, List<SetSpacePermissionDto> grants, int createdByUserId);
    }
}
