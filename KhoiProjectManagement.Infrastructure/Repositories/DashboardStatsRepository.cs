using System.Data;
using Dapper;
using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace KhoiProjectManagement.Infrastructure.Repositories
{
    public class DashboardStatsRepository : IDashboardStatsRepository
    {
        private readonly ProjectManagementContext _context;

        public DashboardStatsRepository(ProjectManagementContext context)
        {
            _context = context;
        }

        public async Task<DashboardCountsResult> GetCountsAsync(DateTime now, IReadOnlyCollection<int>? projectIds = null)
        {
            // @scoped = false counts everything (an administrator / the system). When true, only the listed projects and
            // their tasks are counted, so a dashboard never reveals how much work exists outside the caller's projects.
            // "Overdue" ignores the project-end placeholder date given to items created without a due date.
            const string sql = @"
                SELECT
                    (SELECT COUNT(*) FROM ""Projects"" WHERE (NOT @scoped OR ""Id"" = ANY(@ids))) AS ""TotalProjects"",
                    (SELECT COUNT(*) FROM ""Projects"" WHERE ""Status"" = 'active' AND (NOT @scoped OR ""Id"" = ANY(@ids))) AS ""ActiveProjects"",
                    (SELECT COUNT(*) FROM ""Tasks"" WHERE (NOT @scoped OR ""ProjectId"" = ANY(@ids))) AS ""TotalTasks"",
                    (SELECT COUNT(*) FROM ""Tasks"" WHERE ""Status"" = 'completed' AND (NOT @scoped OR ""ProjectId"" = ANY(@ids))) AS ""CompletedTasks"",
                    (SELECT COUNT(*) FROM ""Tasks"" WHERE ""Status"" = 'in-progress' AND (NOT @scoped OR ""ProjectId"" = ANY(@ids))) AS ""InProgressTasks"",
                    (SELECT COUNT(*) FROM ""Tasks"" WHERE ""Status"" = 'todo' AND (NOT @scoped OR ""ProjectId"" = ANY(@ids))) AS ""TodoTasks"",
                    (SELECT COUNT(*) FROM ""Tasks"" WHERE ""Status"" = 'blocked' AND (NOT @scoped OR ""ProjectId"" = ANY(@ids))) AS ""BlockedTasks"",
                    (SELECT COUNT(*) FROM ""Tasks"" WHERE ""Status"" != 'completed' AND ""HasExplicitDueDate"" AND ""DueDate"" < @now
                        AND (NOT @scoped OR ""ProjectId"" = ANY(@ids))) AS ""OverdueTasks""";

            var connection = _context.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync();
            }
            return await connection.QuerySingleAsync<DashboardCountsResult>(sql, new
            {
                now,
                scoped = projectIds != null,
                ids = projectIds?.ToArray() ?? Array.Empty<int>(),
            });
        }
    }
}
