using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using Microsoft.EntityFrameworkCore;

namespace KhoiProjectManagement.Application
{
    public enum ProjectAbility { View, Create, Edit }

    public enum ProjectAccessOutcome { Allowed, NotVisible, Forbidden }

    // Need-to-know for projects and everything inside them (tasks, attachments, search hits, report rows...). Every
    // service that returns or changes project data asks THIS, so there is one rule instead of a dozen copies:
    //   - an administrator (the Admin role, which carries the "superadmin" claim) sees and may do everything;
    //   - anyone else sees only the projects they are a member of, or created - nothing else exists as far as they can tell;
    //   - what they may DO in such a project follows their project role (the PM role matrix) and is capped at read-only
    //     when an administrator has set their access level to read only.
    // Code running outside a signed-in request (Quartz jobs, seeding) is the system itself and is unrestricted.
    public interface IProjectScope
    {
        Task<bool> IsUnrestrictedAsync();
        Task<IQueryable<Project>> ScopeAsync(IQueryable<Project> projects);
        Task<IQueryable<ProjectTask>> ScopeAsync(IQueryable<ProjectTask> tasks);
        // null = every project (unrestricted caller).
        Task<IReadOnlyCollection<int>?> VisibleProjectIdsAsync();
        Task<ProjectAccessOutcome> CheckAsync(int projectId, ProjectAbility ability);
        // KeyNotFoundException (404) when the project is not visible to the caller - never reveal it exists -
        // ForbiddenException (403) when it is visible but the caller's role or access level does not allow the action.
        Task RequireAsync(int projectId, ProjectAbility ability);
    }

    public class ProjectScope : IProjectScope
    {
        private sealed record Snapshot(bool Unrestricted, Dictionary<int, string> Roles, HashSet<int> Created, bool ReadOnly)
        {
            public IReadOnlyCollection<int> VisibleIds => Roles.Keys.Union(Created).ToList();
        }

        private readonly ICurrentRequestContext _context;
        private readonly IRepository<Project> _projects;
        private readonly IRepository<ProjectUser> _members;
        private readonly IRepository<User> _users;
        private Task<Snapshot>? _snapshot;   // one lookup per request: this service is registered scoped

        public ProjectScope(ICurrentRequestContext context, IRepository<Project> projects, IRepository<ProjectUser> members, IRepository<User> users)
        {
            _context = context;
            _projects = projects;
            _members = members;
            _users = users;
        }

        private Task<Snapshot> LoadAsync() => _snapshot ??= LoadCoreAsync();

        private async Task<Snapshot> LoadCoreAsync()
        {
            if (_context.UserId is not int userId || _context.IsAdministrator)
                return new Snapshot(true, new(), new(), false);

            var roles = (await _members.Query().AsNoTracking().Where(m => m.UserId == userId)
                .Select(m => new { m.ProjectId, m.ProjectRole }).ToListAsync())
                .GroupBy(m => m.ProjectId).ToDictionary(g => g.Key, g => g.First().ProjectRole);
            var created = (await _projects.Query().AsNoTracking().Where(p => p.CreatedBy == userId).Select(p => p.Id).ToListAsync()).ToHashSet();
            var readOnly = await _users.Query().AsNoTracking().Where(u => u.Id == userId).Select(u => u.PmReadOnly).FirstOrDefaultAsync();
            return new Snapshot(false, roles, created, readOnly);
        }

        public async Task<bool> IsUnrestrictedAsync() => (await LoadAsync()).Unrestricted;

        public async Task<IReadOnlyCollection<int>?> VisibleProjectIdsAsync()
        {
            var s = await LoadAsync();
            return s.Unrestricted ? null : s.VisibleIds;
        }

        public async Task<IQueryable<Project>> ScopeAsync(IQueryable<Project> projects)
        {
            var s = await LoadAsync();
            if (s.Unrestricted) return projects;
            var ids = s.VisibleIds;
            return projects.Where(p => ids.Contains(p.Id));
        }

        public async Task<IQueryable<ProjectTask>> ScopeAsync(IQueryable<ProjectTask> tasks)
        {
            var s = await LoadAsync();
            if (s.Unrestricted) return tasks;
            var ids = s.VisibleIds;
            return tasks.Where(t => ids.Contains(t.ProjectId));
        }

        public async Task<ProjectAccessOutcome> CheckAsync(int projectId, ProjectAbility ability)
        {
            var s = await LoadAsync();
            if (s.Unrestricted) return ProjectAccessOutcome.Allowed;

            var isMember = s.Roles.TryGetValue(projectId, out var role);
            if (!isMember && !s.Created.Contains(projectId)) return ProjectAccessOutcome.NotVisible;

            // The creator of a project who is not on its team has no role row; they own it, so they act as its Project Admin.
            role = isMember ? role : ProjectRoles.ProjectAdmin;
            if (ability == ProjectAbility.View) return ProjectAccessOutcome.Allowed;
            if (s.ReadOnly) return ProjectAccessOutcome.Forbidden;

            var action = ability == ProjectAbility.Create ? PmAction.CreateItem : PmAction.EditItem;
            return ProjectRoles.Allows(role, action) ? ProjectAccessOutcome.Allowed : ProjectAccessOutcome.Forbidden;
        }

        public async Task RequireAsync(int projectId, ProjectAbility ability)
        {
            switch (await CheckAsync(projectId, ability))
            {
                case ProjectAccessOutcome.NotVisible:
                    throw new KeyNotFoundException();
                case ProjectAccessOutcome.Forbidden:
                    throw new ForbiddenException("Your role or access level on this project does not allow that.");
            }
        }
    }
}
