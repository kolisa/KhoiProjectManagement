using System.Data;
using Dapper;
using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace KhoiProjectManagement.Infrastructure.Repositories
{
    public class ReportStatsRepository : IReportStatsRepository
    {
        private readonly ProjectManagementContext _context;

        public ReportStatsRepository(ProjectManagementContext context)
        {
            _context = context;
        }

        public async Task<List<ProjectTaskCountsResult>> GetProjectTaskCountsAsync()
        {
            const string sql = @"
                SELECT
                    p.""Name"" AS ""Name"",
                    p.""Status"" AS ""Status"",
                    COUNT(t.""Id"") AS ""TasksCount"",
                    COUNT(t.""Id"") FILTER (WHERE t.""Status"" = 'completed') AS ""CompletedTasks""
                FROM ""Projects"" p
                LEFT JOIN ""Tasks"" t ON t.""ProjectId"" = p.""Id""
                GROUP BY p.""Id"", p.""Name"", p.""Status""
                ORDER BY p.""Id""";

            var connection = await GetOpenConnectionAsync();
            var result = await connection.QueryAsync<ProjectTaskCountsResult>(sql);
            return result.AsList();
        }

        public async Task<List<TeamMemberTaskCountsResult>> GetTeamMemberTaskCountsAsync(DateTime now)
        {
            const string sql = @"
                SELECT
                    u.""Name"" AS ""Name"",
                    u.""Position"" AS ""Position"",
                    COUNT(t.""Id"") AS ""AssignedTasks"",
                    COUNT(t.""Id"") FILTER (WHERE t.""Status"" = 'completed') AS ""CompletedTasks"",
                    COUNT(t.""Id"") FILTER (WHERE t.""Status"" != 'completed' AND t.""DueDate"" < @now) AS ""OverdueTasks""
                FROM ""Users"" u
                LEFT JOIN ""Tasks"" t ON t.""AssignedToId"" = u.""Id""
                WHERE u.""IsActive"" = true
                GROUP BY u.""Id"", u.""Name"", u.""Position""
                ORDER BY u.""Id""";

            var connection = await GetOpenConnectionAsync();
            var result = await connection.QueryAsync<TeamMemberTaskCountsResult>(sql, new { now });
            return result.AsList();
        }

        private async Task<IDbConnection> GetOpenConnectionAsync()
        {
            var connection = _context.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync();
            }
            return connection;
        }
    }
}
