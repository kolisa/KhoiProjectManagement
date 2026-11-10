using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using Microsoft.EntityFrameworkCore;

namespace KhoiProjectManagement.Application
{
    // The one place PM-module authorization is decided, in three layers:
    //   1. module access (User.PmAccess) - may this person use the module at all;
    //   2. the module access LEVEL (User.PmReadOnly) - read only, or read and write. A read-only person can only
    //      ever View, whatever their project roles say. This is the per-user read/write policy;
    //   3. a project membership whose role allows the action.
    // An administrator (SuperAdmin, or a Project Admin - see ResolveCallerAsync) bypasses all three, mirroring the flat-permission handler. Non-members get KeyNotFound (404) rather
    // than 403 so a project's existence is not leaked.
    public interface IProjectAccessService
    {
        Task<bool> HasModuleAccessAsync(PmCaller caller);
        // True for a person with module access whose level has been set to read only. Never true for a SuperAdmin.
        Task<bool> IsReadOnlyAsync(PmCaller caller);
        // Who is calling, in the terms every PM service uses. A SuperAdmin (the "superadmin" claim) and a Project Admin
        // are treated alike: both come back with IsSuperAdmin and IsPmAdmin set, so they see every project, may do
        // anything in it, are never read-only and may grant module access. Resolved from the database on each request,
        // so revoking someone's access or admin role takes effect immediately.
        Task<PmCaller> ResolveCallerAsync(int userId, bool hasSuperAdminClaim, bool hasPmAdminPermission);
        Task<string?> GetRoleAsync(PmCaller caller, int projectId);
        Task RequireAsync(PmCaller caller, int projectId, PmAction action);
    }

    public class ProjectAccessService : IProjectAccessService
    {
        public const string SuperAdminRole = "SuperAdmin";
        public const string ReadOnlyMessage = "Your Project Management access is read-only. Ask an administrator to give you read and write access.";

        private readonly IRepository<ProjectUser> _members;
        private readonly IRepository<User> _users;
        private readonly IRepository<Project> _projects;

        public ProjectAccessService(IRepository<ProjectUser> members, IRepository<User> users, IRepository<Project> projects)
        {
            _members = members;
            _users = users;
            _projects = projects;
        }

        // A Project Admin of at least one project is an administrator of the whole module - but only while they still
        // have module access and have not been set to read only. Those two conditions keep an explicit restriction from a
        // SuperAdmin (revoking access, or capping someone at read only) effective even for a Project Admin.
        public static IQueryable<ProjectUser> ProjectAdminsQuery(IQueryable<ProjectUser> members, int userId) =>
            members.Where(m => m.UserId == userId && m.ProjectRole == ProjectRoles.ProjectAdmin
                               && m.User.IsActive && m.User.PmAccess && !m.User.PmReadOnly);

        public async Task<PmCaller> ResolveCallerAsync(int userId, bool hasSuperAdminClaim, bool hasPmAdminPermission)
        {
            if (hasSuperAdminClaim) return new PmCaller(userId, IsSuperAdmin: true, IsPmAdmin: true);
            if (await ProjectAdminsQuery(_members.Query().AsNoTracking(), userId).AnyAsync())
                return new PmCaller(userId, IsSuperAdmin: true, IsPmAdmin: true);
            return new PmCaller(userId, IsSuperAdmin: false, IsPmAdmin: hasPmAdminPermission);
        }

        // Both module flags in one round trip (this runs on every PM request).
        private async Task<(bool Access, bool ReadOnly)> ModuleStateAsync(PmCaller caller)
        {
            if (caller.IsSuperAdmin) return (true, false);
            var user = await _users.Query().AsNoTracking()
                .Where(u => u.Id == caller.UserId && u.IsActive)
                .Select(u => new { u.PmAccess, u.PmReadOnly })
                .FirstOrDefaultAsync();
            return (user?.PmAccess == true, user?.PmAccess == true && user.PmReadOnly);
        }

        public async Task<bool> HasModuleAccessAsync(PmCaller caller) => (await ModuleStateAsync(caller)).Access;

        public async Task<bool> IsReadOnlyAsync(PmCaller caller) => (await ModuleStateAsync(caller)).ReadOnly;

        private async Task<(string? Role, bool ReadOnly)> ResolveAsync(PmCaller caller, int projectId)
        {
            if (caller.IsSuperAdmin)
                return (await _projects.Query().AnyAsync(p => p.Id == projectId) ? SuperAdminRole : null, false);

            var state = await ModuleStateAsync(caller);
            if (!state.Access) return (null, false);

            var role = await _members.Query().AsNoTracking()
                .Where(m => m.ProjectId == projectId && m.UserId == caller.UserId)
                .Select(m => m.ProjectRole)
                .FirstOrDefaultAsync();
            return (role, state.ReadOnly);
        }

        public async Task<string?> GetRoleAsync(PmCaller caller, int projectId) => (await ResolveAsync(caller, projectId)).Role;

        public async Task RequireAsync(PmCaller caller, int projectId, PmAction action)
        {
            var (role, readOnly) = await ResolveAsync(caller, projectId);
            if (role == null) throw new KeyNotFoundException();
            if (role == SuperAdminRole) return;
            if (!ProjectRoles.Allows(role, action))
                throw new ForbiddenException($"Your project role ({role}) cannot perform this action.");
            if (readOnly && action != PmAction.View)
                throw new ForbiddenException(ReadOnlyMessage);
        }
    }
}
