using KhoiProjectManagement.Models;
using KhoiProjectManagement.Models.DTOs;
using KhoiProjectManagementApi.Data;
using Microsoft.EntityFrameworkCore;

namespace KhoiProjectManagementApi.Services
{
    public class RoleService : IRoleService
    {
        private const string ManageRolesPermission = "users.manage_roles";

        private readonly ProjectManagementContext _context;

        public RoleService(ProjectManagementContext context)
        {
            _context = context;
        }

        public async Task<List<RoleDto>> GetRolesAsync()
        {
            var roles = await _context.Roles.ToListAsync();
            return roles.Select(MapRole).ToList();
        }

        public async Task<List<PermissionDto>> GetAllPermissionsAsync()
        {
            var permissions = await _context.Permissions.OrderBy(p => p.Name).ToListAsync();
            return permissions.Select(p => new PermissionDto
            {
                Id = p.Id,
                Resource = p.Resource,
                Action = p.Action,
                Name = p.Name,
                Description = p.Description
            }).ToList();
        }

        public async Task<List<string>?> GetRolePermissionsAsync(int roleId)
        {
            var roleExists = await _context.Roles.AnyAsync(r => r.Id == roleId);
            if (!roleExists)
                return null;

            return await _context.RolePermissions
                .Where(rp => rp.RoleId == roleId)
                .Join(_context.Permissions, rp => rp.PermissionId, p => p.Id, (rp, p) => p.Name)
                .ToListAsync();
        }

        public async Task<bool> SetRolePermissionsAsync(int roleId, List<string> permissionNames, int callerId)
        {
            var role = await _context.Roles.FirstOrDefaultAsync(r => r.Id == roleId);
            if (role == null)
                return false;

            var allPermissions = await _context.Permissions.ToListAsync();
            var newPermissionIds = allPermissions
                .Where(p => permissionNames.Contains(p.Name))
                .Select(p => p.Id)
                .ToHashSet();

            // Self-lockout guard, mirroring SpaceService.SetSpacePermissionsAsync's fix for the same
            // class of bug in Phase 3: simulate the caller's effective permission set after this change
            // (using the NEW list for the role being edited, current lists for every other role the
            // caller holds) and block if users.manage_roles would disappear entirely for them.
            var callerRoleIds = await _context.UserRoles
                .Where(ur => ur.UserId == callerId)
                .Select(ur => ur.RoleId)
                .ToListAsync();

            if (callerRoleIds.Contains(roleId))
            {
                var manageRolesPermissionId = allPermissions.FirstOrDefault(p => p.Name == ManageRolesPermission)?.Id;
                var wouldRetain = manageRolesPermissionId.HasValue && newPermissionIds.Contains(manageRolesPermissionId.Value);

                if (!wouldRetain)
                {
                    var otherRoleIds = callerRoleIds.Where(id => id != roleId).ToList();
                    var hasViaOtherRole = otherRoleIds.Count > 0 && await _context.RolePermissions
                        .Where(rp => otherRoleIds.Contains(rp.RoleId))
                        .Join(_context.Permissions, rp => rp.PermissionId, p => p.Id, (rp, p) => p.Name)
                        .AnyAsync(name => name == ManageRolesPermission);

                    if (!hasViaOtherRole)
                        throw new InvalidOperationException(
                            $"This change would remove '{ManageRolesPermission}' from every role you hold, locking you out of role management.");
                }
            }

            var existing = _context.RolePermissions.Where(rp => rp.RoleId == roleId);
            _context.RolePermissions.RemoveRange(existing);

            foreach (var permissionId in newPermissionIds)
            {
                _context.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = permissionId });
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<RoleDto> CreateRoleAsync(CreateRoleDto dto)
        {
            var role = new Role
            {
                Name = dto.Name,
                Description = dto.Description,
                IsSystemRole = false
            };

            _context.Roles.Add(role);
            await _context.SaveChangesAsync();

            return MapRole(role);
        }

        public async Task<bool> UpdateRoleAsync(int id, UpdateRoleDto dto)
        {
            var role = await _context.Roles.FirstOrDefaultAsync(r => r.Id == id);
            if (role == null)
                return false;

            if (role.IsSystemRole)
                throw new InvalidOperationException("System roles (Admin/Manager/Member) cannot be renamed.");

            role.Name = dto.Name;
            role.Description = dto.Description;

            await _context.SaveChangesAsync();
            return true;
        }

        private static RoleDto MapRole(Role role) => new()
        {
            Id = role.Id,
            Name = role.Name,
            Description = role.Description,
            IsSystemRole = role.IsSystemRole
        };
    }
}
