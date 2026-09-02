namespace KhoiProjectManagement.Application.Abstractions
{
    // A deliberate, narrow escape hatch from the generic IRepository<T> pattern, same reasoning as
    // IWikiSearchRepository: DashboardService.GetDashboardStatisticsAsync only ever needed eight counts,
    // but the LINQ version pulled every Project and every Task row into memory to compute them. One SQL
    // statement with scalar subqueries replaces that - Dapper, not EF, since this is pure aggregation
    // with no entity shape to project into.
    public interface IDashboardStatsRepository
    {
        // now is passed in (rather than the query using Postgres's own NOW()) so "overdue" here matches
        // ProjectTask.IsOverdue's exact semantics (DateTime.Now, not UtcNow - a pre-existing quirk this
        // isn't the place to silently fix) regardless of the DB server's timezone configuration.
        Task<DashboardCountsResult> GetCountsAsync(DateTime now);
    }

    public class DashboardCountsResult
    {
        public int TotalProjects { get; set; }
        public int ActiveProjects { get; set; }
        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int InProgressTasks { get; set; }
        public int TodoTasks { get; set; }
        public int BlockedTasks { get; set; }
        public int OverdueTasks { get; set; }
    }
}
