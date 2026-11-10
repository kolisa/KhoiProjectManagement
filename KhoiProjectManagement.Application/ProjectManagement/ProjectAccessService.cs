using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using Microsoft.EntityFrameworkCore;

namespace KhoiProjectManagement.Application
{
    // The one place PM-module authorization is decided: module access (User.PmAccess) AND a project
    // membership whose role allows the action. SuperAdmin bypasses both, mirroring the flat-permission
    // handler. Non-members get KeyNotFound (404) rather than 403 so a project's existence is not leaked.
    public interface IProjectAccessService
    {
        Task<bool> HasModuleAccessAsync(PmCaller caller);
        Task<string?> GetRoleAsync(PmCaller caller, int projectId);
        Task RequireAsync(PmCaller caller, int projectId, PmAction action);
    }

    public class ProjectAccessService : IProjectAccessService
    {
        public const string SuperAdminRole = "SuperAdmin";
        private readonly IRepository<ProjectUser> _members;
        private readonly IRepository<User> _users;
        private readonly IRepository<Project> _projects;

        public ProjectAccessService(IRepository<ProjectUser> members, IRepository<User> users, IRepository<Project> projects)
        {
            _members = members;
            _users = users;
            _projects = projects;
        }

        public async Task<bool> HasModuleAccessAsync(PmCaller caller)
        {
            if (caller.IsSuperAdmin) return true;
            return await _users.Query().AnyAsync(u => u.Id == caller.UserId && u.IsActive && u.PmAccess);
        }

        public async Task<string?> GetRoleAsync(PmCaller caller, int projectId)
        {
            if (caller.IsSuperAdmin)
                return await _projects.Query().AnyAsync(p => p.Id == projectId) ? SuperAdminRole : null;
            if (!await HasModuleAccessAsync(caller)) return null;
            return await _members.Query().AsNoTracking()
                .Where(m => m.ProjectId == projectId && m.UserId == caller.UserId)
                .Select(m => m.ProjectRole)
                .FirstOrDefaultAsync();
        }

        public async Task RequireAsync(PmCaller caller, int projectId, PmAction action)
        {
            var role = await GetRoleAsync(caller, projectId) ?? throw new KeyNotFoundException();
            if (role == SuperAdminRole) return;
            if (!ProjectRoles.Allows(role, action))
                throw new ForbiddenException($"Your project role ({role}) cannot perform this action.");
        }
    }
}
