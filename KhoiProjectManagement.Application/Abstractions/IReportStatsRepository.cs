namespace KhoiProjectManagement.Application.Abstractions
{
    // A deliberate, narrow escape hatch from the generic IRepository<T> pattern, same reasoning as
    // IWikiSearchRepository/IDashboardStatsRepository: both report methods here only ever needed
    // per-project/per-user task counts, but the LINQ version loaded full Project/User graphs (with
    // every Task row via .Include) just to count them in C#. GROUP BY replaces that in one round trip
    // each. CompletionRate is deliberately NOT computed here - ReportService derives it from the raw
    // counts, same as it always has, keeping that (trivial) arithmetic out of the persistence layer.
    public interface IReportStatsRepository
    {
        Task<List<ProjectTaskCountsResult>> GetProjectTaskCountsAsync();

        // now is passed in rather than the query using Postgres's own NOW() so "overdue" matches
        // ProjectTask.IsOverdue's exact semantics (DateTime.Now, not UtcNow) regardless of the DB
        // server's timezone configuration - same reasoning as IDashboardStatsRepository.
        Task<List<TeamMemberTaskCountsResult>> GetTeamMemberTaskCountsAsync(DateTime now);
    }

    public class ProjectTaskCountsResult
    {
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int TasksCount { get; set; }
        public int CompletedTasks { get; set; }
    }

    public class TeamMemberTaskCountsResult
    {
        public string Name { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;
        public int AssignedTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int OverdueTasks { get; set; }
    }
}
