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

        public async Task<DashboardCountsResult> GetCountsAsync(DateTime now)
        {
            const string sql = @"
                SELECT
                    (SELECT COUNT(*) FROM ""Projects"") AS ""TotalProjects"",
                    (SELECT COUNT(*) FROM ""Projects"" WHERE ""Status"" = 'active') AS ""ActiveProjects"",
                    (SELECT COUNT(*) FROM ""Tasks"") AS ""TotalTasks"",
                    (SELECT COUNT(*) FROM ""Tasks"" WHERE ""Status"" = 'completed') AS ""CompletedTasks"",
                    (SELECT COUNT(*) FROM ""Tasks"" WHERE ""Status"" = 'in-progress') AS ""InProgressTasks"",
                    (SELECT COUNT(*) FROM ""Tasks"" WHERE ""Status"" = 'todo') AS ""TodoTasks"",
                    (SELECT COUNT(*) FROM ""Tasks"" WHERE ""Status"" = 'blocked') AS ""BlockedTasks"",
                    (SELECT COUNT(*) FROM ""Tasks"" WHERE ""Status"" != 'completed' AND ""DueDate"" < @now) AS ""OverdueTasks""";

            var connection = _context.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync();
            }
            return await connection.QuerySingleAsync<DashboardCountsResult>(sql, new { now });
        }
    }
}
