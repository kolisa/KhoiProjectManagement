using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using Microsoft.EntityFrameworkCore;

namespace KhoiProjectManagement.Application
{
    public interface IPmProjectService
    {
        Task<List<PmProjectSummaryDto>> GetMyProjectsAsync(PmCaller caller);
        Task<List<PmMemberDto>> GetMembersAsync(PmCaller caller, int projectId);
        Task SetMemberAsync(PmCaller caller, int projectId, int userId, string role);
        Task RemoveMemberAsync(PmCaller caller, int projectId, int userId);
        Task<List<PmUserLookupDto>> GetAssignableUsersAsync(PmCaller caller);

        // SuperAdmin module-access administration (controller gates on the pm.admin permission).
        Task<List<PmAccessUserDto>> GetAccessUsersAsync();
        Task SetAccessAsync(int actingUserId, int userId, SetPmAccessDto dto);
    }

    public class PmProjectService : IPmProjectService
    {
        private readonly IRepository<Project> _projects;
        private readonly IRepository<ProjectUser> _members;
        private readonly IRepository<ProjectTask> _tasks;
        private readonly IRepository<User> _users;
        private readonly IProjectAccessService _access;
        private readonly IActivityLogService _activity;
        private readonly IUnitOfWork _unitOfWork;

        public PmProjectService(
            IRepository<Project> projects,
            IRepository<ProjectUser> members,
            IRepository<ProjectTask> tasks,
            IRepository<User> users,
            IProjectAccessService access,
            IActivityLogService activity,
            IUnitOfWork unitOfWork)
        {
            _projects = projects;
            _members = members;
            _tasks = tasks;
            _users = users;
            _access = access;
            _activity = activity;
            _unitOfWork = unitOfWork;
        }

        public async Task<List<PmProjectSummaryDto>> GetMyProjectsAsync(PmCaller caller)
        {
            if (!await _access.HasModuleAccessAsync(caller))
                throw new ForbiddenException("You do not have access to the Project Management module.");

            var projects = _projects.Query().AsNoTracking();
            if (!caller.IsSuperAdmin)
                projects = projects.Where(p => p.ProjectUsers.Any(m => m.UserId == caller.UserId));

            var rows = await projects.OrderBy(p => p.Name).Select(p => new
            {
                p.Id,
                p.Name,
                p.Status,
                Role = p.ProjectUsers.Where(m => m.UserId == caller.UserId).Select(m => m.ProjectRole).FirstOrDefault(),
                Total = p.Tasks.Count(),
                Open = p.Tasks.Count(t => t.Status != "completed"),
            }).ToListAsync();

            return rows.Select(r => new PmProjectSummaryDto
            {
                Id = r.Id,
                Name = r.Name,
                Status = r.Status,
                MyRole = caller.IsSuperAdmin ? ProjectAccessService.SuperAdminRole : r.Role ?? string.Empty,
                TotalItems = r.Total,
                OpenItems = r.Open,
            }).ToList();
        }

        public async Task<List<PmMemberDto>> GetMembersAsync(PmCaller caller, int projectId)
        {
            await _access.RequireAsync(caller, projectId, PmAction.View);
            return await _members.Query().AsNoTracking()
                .Where(m => m.ProjectId == projectId)
                .OrderBy(m => m.User.Name)
                .Select(m => new PmMemberDto { UserId = m.UserId, Name = m.User.Name, Email = m.User.Email, Role = m.ProjectRole })
                .ToListAsync();
        }

        public async Task SetMemberAsync(PmCaller caller, int projectId, int userId, string role)
        {
            await _access.RequireAsync(caller, projectId, PmAction.ManageMembers);

            var target = await _users.Query().AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId && u.IsActive)
                ?? throw new InvalidOperationException("User not found or inactive.");
            if (!target.PmAccess)
                throw new InvalidOperationException("This user has not been granted Project Management module access. Ask a SuperAdmin to enable it first.");

            var existing = await _members.Query().FirstOrDefaultAsync(m => m.ProjectId == projectId && m.UserId == userId);
            if (existing == null)
                _members.Add(new ProjectUser { ProjectId = projectId, UserId = userId, ProjectRole = role });
            else
            {
                if (existing.ProjectRole == ProjectRoles.ProjectAdmin && role != ProjectRoles.ProjectAdmin)
                    await EnsureAnotherAdminAsync(projectId, userId);
                existing.ProjectRole = role;
            }

            await _unitOfWork.SaveChangesAsync();
            await _activity.LogAsync("Project", projectId, $"{target.Name} -> {role}", caller.UserId, "member-role-set");
        }

        public async Task RemoveMemberAsync(PmCaller caller, int projectId, int userId)
        {
            await _access.RequireAsync(caller, projectId, PmAction.ManageMembers);

            var existing = await _members.Query().Include(m => m.User)
                .FirstOrDefaultAsync(m => m.ProjectId == projectId && m.UserId == userId)
                ?? throw new KeyNotFoundException();

            if (existing.ProjectRole == ProjectRoles.ProjectAdmin)
                await EnsureAnotherAdminAsync(projectId, userId);

            // Their open items shouldn't point at someone who is no longer on the project.
            var assigned = await _tasks.Query().Where(t => t.ProjectId == projectId && t.AssignedToId == userId).ToListAsync();
            foreach (var task in assigned) task.AssignedToId = null;

            _members.Remove(existing);
            await _unitOfWork.SaveChangesAsync();
            await _activity.LogAsync("Project", projectId, existing.User.Name, caller.UserId, "member-removed");
        }

        public async Task<List<PmUserLookupDto>> GetAssignableUsersAsync(PmCaller caller)
        {
            if (!await _access.HasModuleAccessAsync(caller))
                throw new ForbiddenException("You do not have access to the Project Management module.");
            return await _users.Query().AsNoTracking()
                .Where(u => u.IsActive && u.PmAccess)
                .OrderBy(u => u.Name)
                .Select(u => new PmUserLookupDto { Id = u.Id, Name = u.Name, Email = u.Email })
                .ToListAsync();
        }

        public async Task<List<PmAccessUserDto>> GetAccessUsersAsync() =>
            await _users.Query().AsNoTracking()
                .Where(u => u.IsActive)
                .OrderBy(u => u.Name)
                .Select(u => new PmAccessUserDto { Id = u.Id, Name = u.Name, Email = u.Email, PmAccess = u.PmAccess, PmOnly = u.PmOnly })
                .ToListAsync();

        public async Task SetAccessAsync(int actingUserId, int userId, SetPmAccessDto dto)
        {
            var user = await _users.Query().FirstOrDefaultAsync(u => u.Id == userId) ?? throw new KeyNotFoundException();

            // Module-only confinement is meaningless (and would lock the account out of everything)
            // without module access itself.
            if (dto.PmOnly && !dto.PmAccess)
                throw new InvalidOperationException("A module-only user must also have Project Management access.");

            user.PmAccess = dto.PmAccess;
            user.PmOnly = dto.PmOnly;
            await _unitOfWork.SaveChangesAsync();
            await _activity.LogAsync("User", user.Id, user.Name, actingUserId, "pm-access-set", $"access={dto.PmAccess}, moduleOnly={dto.PmOnly}");
        }

        private async Task EnsureAnotherAdminAsync(int projectId, int excludingUserId)
        {
            var other = await _members.Query().AnyAsync(m =>
                m.ProjectId == projectId && m.UserId != excludingUserId && m.ProjectRole == ProjectRoles.ProjectAdmin);
            if (!other)
                throw new InvalidOperationException("A project must keep at least one Project Admin.");
        }
    }
}
