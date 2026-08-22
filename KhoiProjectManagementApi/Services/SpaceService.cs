using System.Security.Claims;
using KhoiProjectManagement.Models;
using KhoiProjectManagement.Models.DTOs;
using KhoiProjectManagementApi.Data;
using Microsoft.EntityFrameworkCore;

namespace KhoiProjectManagementApi.Services
{
    public class SpaceService : ISpaceService
    {
        private const string ProjectsRootSpaceName = "Projects";

        private readonly ProjectManagementContext _context;
        private readonly ISpacePermissionResolver _resolver;

        public SpaceService(ProjectManagementContext context, ISpacePermissionResolver resolver)
        {
            _context = context;
            _resolver = resolver;
        }

        public async Task<int> EnsureProjectSpaceAsync(int projectId, int createdByUserId)
        {
            var project = await _context.Projects.FindAsync(projectId);
            if (project == null)
                throw new InvalidOperationException($"Project {projectId} not found");

            if (project.SpaceId.HasValue)
                return project.SpaceId.Value;

            var rootSpace = await _context.Spaces.FirstOrDefaultAsync(
                s => s.ParentSpaceId == null && s.SpaceType == SpaceType.ProjectSpace && s.Name == ProjectsRootSpaceName);

            if (rootSpace == null)
            {
                rootSpace = new Space
                {
                    Name = ProjectsRootSpaceName,
                    SpaceType = SpaceType.ProjectSpace,
                    CreatedBy = createdByUserId
                };
                _context.Spaces.Add(rootSpace);
                await _context.SaveChangesAsync();
            }

            var projectSpace = new Space
            {
                Name = project.Name,
                ParentSpaceId = rootSpace.Id,
                SpaceType = SpaceType.ProjectSpace,
                CreatedBy = createdByUserId
            };
            _context.Spaces.Add(projectSpace);
            await _context.SaveChangesAsync();

            project.SpaceId = projectSpace.Id;
            await _context.SaveChangesAsync();

            _resolver.InvalidateCache();
            return projectSpace.Id;
        }

        public async Task SyncSpaceMembersAsync(int spaceId, IEnumerable<int> userIds, PermissionLevel level, int createdByUserId)
        {
            var userIdSet = new HashSet<int>(userIds);

            var existingUserGrants = await _context.SpacePermissions
                .Where(sp => sp.SpaceId == spaceId && sp.UserId != null)
                .ToListAsync();

            _context.SpacePermissions.RemoveRange(existingUserGrants);

            _context.SpacePermissions.AddRange(userIdSet.Select(userId => new SpacePermission
            {
                SpaceId = spaceId,
                UserId = userId,
                Level = level,
                CreatedBy = createdByUserId
            }));

            await _context.SaveChangesAsync();
            _resolver.InvalidateCache();
        }

        public async Task<List<SpaceDto>> GetSpacesAsync(int? parentSpaceId, ClaimsPrincipal caller)
        {
            var spaces = await _context.Spaces
                .Include(s => s.Creator)
                .Where(s => s.IsActive && s.ParentSpaceId == parentSpaceId)
                .OrderBy(s => s.Name)
                .ToListAsync();

            var (userId, roleIds) = GetCallerIdentity(caller);
            var result = new List<SpaceDto>();
            foreach (var space in spaces)
            {
                var level = await _resolver.ResolveEffectiveLevelAsync(space.Id, userId, roleIds);
                if (level.HasValue)
                {
                    result.Add(MapToDto(space, level.Value));
                }
            }
            return result;
        }

        public async Task<SpaceDto?> GetSpaceByIdAsync(int id, ClaimsPrincipal caller)
        {
            var space = await _context.Spaces.Include(s => s.Creator).FirstOrDefaultAsync(s => s.Id == id);
            if (space == null)
                return null;

            var (userId, roleIds) = GetCallerIdentity(caller);
            var level = await _resolver.ResolveEffectiveLevelAsync(space.Id, userId, roleIds);
            return level.HasValue ? MapToDto(space, level.Value) : null;
        }

        public async Task<SpaceDto> CreateSpaceAsync(CreateSpaceDto dto, int createdByUserId)
        {
            if (!Enum.TryParse<SpaceType>(dto.SpaceType, ignoreCase: true, out var spaceType))
            {
                throw new InvalidOperationException($"Invalid SpaceType '{dto.SpaceType}'");
            }

            var space = new Space
            {
                Name = dto.Name,
                Description = dto.Description,
                ParentSpaceId = dto.ParentSpaceId,
                SpaceType = spaceType,
                InheritPermissions = dto.InheritPermissions,
                CreatedBy = createdByUserId
            };

            _context.Spaces.Add(space);
            await _context.SaveChangesAsync();

            // Without this, a brand-new root Space is orphaned - nobody, not even its creator, could
            // have any grant on it yet, since there's no ancestor to inherit from.
            _context.SpacePermissions.Add(new SpacePermission
            {
                SpaceId = space.Id,
                UserId = createdByUserId,
                Level = PermissionLevel.Manage,
                CreatedBy = createdByUserId
            });
            await _context.SaveChangesAsync();
            _resolver.InvalidateCache();

            var creator = await _context.Users.FindAsync(createdByUserId);
            return new SpaceDto
            {
                Id = space.Id,
                Name = space.Name,
                Description = space.Description,
                ParentSpaceId = space.ParentSpaceId,
                SpaceType = space.SpaceType.ToString(),
                InheritPermissions = space.InheritPermissions,
                CreatorName = creator?.Name ?? "Unknown",
                CreatedAt = space.CreatedAt,
                IsActive = space.IsActive,
                MyEffectiveLevel = PermissionLevel.Manage.ToString() // the creator was just granted Manage above
            };
        }

        public async Task<bool> UpdateSpaceAsync(int id, UpdateSpaceDto dto)
        {
            var space = await _context.Spaces.FindAsync(id);
            if (space == null)
                return false;

            space.Name = dto.Name;
            space.Description = dto.Description;
            space.InheritPermissions = dto.InheritPermissions;

            await _context.SaveChangesAsync();
            _resolver.InvalidateCache();
            return true;
        }

        public async Task<bool> DeleteSpaceAsync(int id)
        {
            var space = await _context.Spaces.FindAsync(id);
            if (space == null)
                return false;

            var hasChildren = await _context.Spaces.AnyAsync(s => s.ParentSpaceId == id);
            var hasVaultEntries = await _context.VaultEntries.AnyAsync(v => v.SpaceId == id);
            if (hasChildren || hasVaultEntries)
            {
                throw new InvalidOperationException("Cannot delete a Space that still has child Spaces or entries - move or remove them first.");
            }

            space.IsActive = false;
            await _context.SaveChangesAsync();
            _resolver.InvalidateCache();
            return true;
        }

        public async Task<List<SpacePermissionDto>> GetSpacePermissionsAsync(int spaceId)
        {
            var grants = await _context.SpacePermissions
                .Include(sp => sp.Role)
                .Include(sp => sp.User)
                .Where(sp => sp.SpaceId == spaceId)
                .ToListAsync();

            return grants.Select(sp => new SpacePermissionDto
            {
                Id = sp.Id,
                RoleId = sp.RoleId,
                RoleName = sp.Role?.Name,
                UserId = sp.UserId,
                UserName = sp.User?.Name,
                Level = sp.Level.ToString()
            }).ToList();
        }

        public async Task<bool> SetSpacePermissionsAsync(int spaceId, List<SetSpacePermissionDto> grants, int createdByUserId)
        {
            var space = await _context.Spaces.FindAsync(spaceId);
            if (space == null)
                return false;

            foreach (var grant in grants)
            {
                var granteeCount = (grant.RoleId.HasValue ? 1 : 0) + (grant.UserId.HasValue ? 1 : 0);
                if (granteeCount != 1)
                {
                    throw new InvalidOperationException("Each grant must target exactly one of RoleId or UserId.");
                }
                if (!Enum.TryParse<PermissionLevel>(grant.Level, ignoreCase: true, out _))
                {
                    throw new InvalidOperationException($"Invalid permission level '{grant.Level}'");
                }
            }

            var existingGrants = await _context.SpacePermissions.Where(sp => sp.SpaceId == spaceId).ToListAsync();
            _context.SpacePermissions.RemoveRange(existingGrants);

            var newGrants = grants.Select(g => new SpacePermission
            {
                SpaceId = spaceId,
                RoleId = g.RoleId,
                UserId = g.UserId,
                Level = Enum.Parse<PermissionLevel>(g.Level, ignoreCase: true),
                CreatedBy = createdByUserId
            }).ToList();

            // This is a full-replace PUT, but the caller only reached this method because they already
            // hold Manage on this Space (checked by SpacesController before calling in) - a replacement
            // list that omits them would silently lock them (and potentially everyone) out with no
            // recovery path. Always keep their own Manage grant, regardless of what was submitted.
            var callerRetainsManage = newGrants.Any(g => g.UserId == createdByUserId && g.Level == PermissionLevel.Manage);
            if (!callerRetainsManage)
            {
                newGrants.Add(new SpacePermission
                {
                    SpaceId = spaceId,
                    UserId = createdByUserId,
                    Level = PermissionLevel.Manage,
                    CreatedBy = createdByUserId
                });
            }

            _context.SpacePermissions.AddRange(newGrants);

            await _context.SaveChangesAsync();
            _resolver.InvalidateCache();
            return true;
        }

        private static SpaceDto MapToDto(Space space, PermissionLevel myEffectiveLevel)
        {
            return new SpaceDto
            {
                Id = space.Id,
                Name = space.Name,
                Description = space.Description,
                ParentSpaceId = space.ParentSpaceId,
                SpaceType = space.SpaceType.ToString(),
                InheritPermissions = space.InheritPermissions,
                CreatorName = space.Creator?.Name ?? "Unknown",
                CreatedAt = space.CreatedAt,
                IsActive = space.IsActive,
                MyEffectiveLevel = myEffectiveLevel.ToString()
            };
        }

        private static (int UserId, List<int> RoleIds) GetCallerIdentity(ClaimsPrincipal caller)
        {
            var userIdClaim = caller.FindFirst(ClaimTypes.NameIdentifier)
                ?? throw new InvalidOperationException("Caller has no NameIdentifier claim.");
            var userId = int.Parse(userIdClaim.Value);

            var roleIds = caller.FindAll("roleId")
                .Select(c => int.TryParse(c.Value, out var id) ? id : (int?)null)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .ToList();

            return (userId, roleIds);
        }
    }
}
