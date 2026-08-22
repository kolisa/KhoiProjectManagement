using KhoiProjectManagement.Models.DTOs;

namespace KhoiProjectManagementApi.Services
{
    public interface IRoleService
    {
        Task<List<RoleDto>> GetRolesAsync();
        Task<List<PermissionDto>> GetAllPermissionsAsync();
        Task<List<string>?> GetRolePermissionsAsync(int roleId);

        // Throws InvalidOperationException if applying this change would leave the calling user
        // without users.manage_roles through any of their roles (the self-lockout guard).
        Task<bool> SetRolePermissionsAsync(int roleId, List<string> permissionNames, int callerId);

        Task<RoleDto> CreateRoleAsync(CreateRoleDto dto);
        Task<bool> UpdateRoleAsync(int id, UpdateRoleDto dto);
    }
}
