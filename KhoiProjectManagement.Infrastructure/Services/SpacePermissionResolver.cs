using KhoiProjectManagement.Application;
using KhoiProjectManagement.Domain;
using KhoiProjectManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace KhoiProjectManagement.Infrastructure.Services
{
    public class SpacePermissionResolver : ISpacePermissionResolver
    {
        private const string CacheKey = "space-permission-snapshot";
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

        private readonly ProjectManagementContext _context;
        private readonly IMemoryCache _cache;

        public SpacePermissionResolver(ProjectManagementContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        public async Task<PermissionLevel?> ResolveEffectiveLevelAsync(int spaceId, int userId, IEnumerable<int> roleIds, IEnumerable<int> groupIds)
        {
            var snapshot = await GetSnapshotAsync();
            var roleIdSet = new HashSet<int>(roleIds);
            var groupIdSet = new HashSet<int>(groupIds);

            // Unconditional bypass for the seeded Admin role (Role.IsSuperAdmin) - this resolver is the
            // single choke point behind both SpacePermissionAuthorizationHandler's gate AND every list
            // view (SpaceService.GetSpacesAsync, WikiService's list filtering, VaultService/LibraryService
            // via RequireSpaceAccessAsync), so patching it here covers all of them without needing an
            // admin concept anywhere else. Returns the max level, ignoring grants/inheritance entirely.
            if (roleIdSet.Overlaps(snapshot.SuperAdminRoleIds))
            {
                return PermissionLevel.Manage;
            }

            int? current = spaceId;
            while (current.HasValue)
            {
                if (!snapshot.Spaces.TryGetValue(current.Value, out var space))
                {
                    return null; // Space doesn't exist (or was deleted) - deny.
                }

                if (snapshot.GrantsBySpaceId.TryGetValue(current.Value, out var grants))
                {
                    var matching = grants
                        .Where(g => g.UserId == userId
                            || (g.RoleId.HasValue && roleIdSet.Contains(g.RoleId.Value))
                            || (g.GroupId.HasValue && groupIdSet.Contains(g.GroupId.Value)))
                        .ToList();

                    if (matching.Count > 0)
                    {
                        return matching.Max(g => g.Level);
                    }
                }

                if (!space.InheritPermissions)
                {
                    return null; // Hard boundary with no local grant - deny, stop walking.
                }

                current = space.ParentSpaceId;
            }

            return null; // Reached the root with no grant found - deny.
        }

        public void InvalidateCache()
        {
            _cache.Remove(CacheKey);
        }

        private async Task<Snapshot> GetSnapshotAsync()
        {
            if (_cache.TryGetValue(CacheKey, out Snapshot? cached) && cached != null)
            {
                return cached;
            }

            var spaces = await _context.Spaces
                .AsNoTracking()
                .Select(s => new { s.Id, s.ParentSpaceId, s.InheritPermissions })
                .ToListAsync();

            var grants = await _context.SpacePermissions
                .AsNoTracking()
                .Select(sp => new { sp.SpaceId, sp.RoleId, sp.UserId, sp.GroupId, sp.Level })
                .ToListAsync();

            var superAdminRoleIds = await _context.Roles
                .AsNoTracking()
                .Where(r => r.IsSuperAdmin)
                .Select(r => r.Id)
                .ToListAsync();

            var snapshot = new Snapshot
            {
                Spaces = spaces.ToDictionary(s => s.Id, s => (s.ParentSpaceId, s.InheritPermissions)),
                GrantsBySpaceId = grants
                    .GroupBy(g => g.SpaceId)
                    .ToDictionary(
                        group => group.Key,
                        group => group.Select(g => (g.RoleId, g.UserId, g.GroupId, g.Level)).ToList()),
                SuperAdminRoleIds = new HashSet<int>(superAdminRoleIds)
            };

            _cache.Set(CacheKey, snapshot, CacheDuration);
            return snapshot;
        }

        private class Snapshot
        {
            public Dictionary<int, (int? ParentSpaceId, bool InheritPermissions)> Spaces { get; set; } = new();
            public Dictionary<int, List<(int? RoleId, int? UserId, int? GroupId, PermissionLevel Level)>> GrantsBySpaceId { get; set; } = new();
            public HashSet<int> SuperAdminRoleIds { get; set; } = new();
        }
    }
}
