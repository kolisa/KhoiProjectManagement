using KhoiProjectManagement.Application;
using KhoiProjectManagement.Domain;

namespace KhoiProjectManagement.UnitTests.Services
{
    // For service tests that are about something other than who may see what: the scope of an administrator / the
    // system, which restricts nothing. Tests of the restriction itself use a real ProjectScope (see ProjectScopeTests).
    public sealed class AllowAllProjectScope : IProjectScope
    {
        public Task<bool> IsUnrestrictedAsync() => Task.FromResult(true);
        public Task<IQueryable<Project>> ScopeAsync(IQueryable<Project> projects) => Task.FromResult(projects);
        public Task<IQueryable<ProjectTask>> ScopeAsync(IQueryable<ProjectTask> tasks) => Task.FromResult(tasks);
        public Task<IReadOnlyCollection<int>?> VisibleProjectIdsAsync() => Task.FromResult<IReadOnlyCollection<int>?>(null);
        public Task<ProjectAccessOutcome> CheckAsync(int projectId, ProjectAbility ability) => Task.FromResult(ProjectAccessOutcome.Allowed);
        public Task RequireAsync(int projectId, ProjectAbility ability) => Task.CompletedTask;
    }
}
