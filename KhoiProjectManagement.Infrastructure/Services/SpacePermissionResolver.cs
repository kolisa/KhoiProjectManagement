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

        public async Task<PermissionLevel?> ResolveEffectiveLevelAsync(int spaceId, int userId, IEnumerable<int> roleIds)
        {
            var snapshot = await GetSnapshotAsync();
            var roleIdSet = new HashSet<int>(roleIds);

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
                        .Where(g => g.UserId == userId || (g.RoleId.HasValue && roleIdSet.Contains(g.RoleId.Value)))
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
                .Select(sp => new { sp.SpaceId, sp.RoleId, sp.UserId, sp.Level })
                .ToListAsync();

            var snapshot = new Snapshot
            {
                Spaces = spaces.ToDictionary(s => s.Id, s => (s.ParentSpaceId, s.InheritPermissions)),
                GrantsBySpaceId = grants
                    .GroupBy(g => g.SpaceId)
                    .ToDictionary(
                        group => group.Key,
                        group => group.Select(g => (g.RoleId, g.UserId, g.Level)).ToList())
            };

            _cache.Set(CacheKey, snapshot, CacheDuration);
            return snapshot;
        }

        private class Snapshot
        {
            public Dictionary<int, (int? ParentSpaceId, bool InheritPermissions)> Spaces { get; set; } = new();
            public Dictionary<int, List<(int? RoleId, int? UserId, PermissionLevel Level)>> GrantsBySpaceId { get; set; } = new();
        }
    }
}
