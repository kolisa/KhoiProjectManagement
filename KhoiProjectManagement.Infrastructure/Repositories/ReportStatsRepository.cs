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

        // projectIds = null: everything (an administrator / the system). Otherwise only those projects - a report must not
        // list the name or workload of a project the caller is not part of.
        public async Task<List<ProjectTaskCountsResult>> GetProjectTaskCountsAsync(IReadOnlyCollection<int>? projectIds = null)
        {
            const string sql = @"
                SELECT
                    p.""Name"" AS ""Name"",
                    p.""Status"" AS ""Status"",
                    COUNT(t.""Id"") AS ""TasksCount"",
                    COUNT(t.""Id"") FILTER (WHERE t.""Status"" = 'completed') AS ""CompletedTasks""
                FROM ""Projects"" p
                LEFT JOIN ""Tasks"" t ON t.""ProjectId"" = p.""Id""
                WHERE (NOT @scoped OR p.""Id"" = ANY(@ids))
                GROUP BY p.""Id"", p.""Name"", p.""Status""
                ORDER BY p.""Id""";

            var connection = await GetOpenConnectionAsync();
            var result = await connection.QueryAsync<ProjectTaskCountsResult>(sql, ScopeArgs(projectIds));
            return result.AsList();
        }

        // Scoped, this counts only tasks inside the listed projects and lists only people who belong to one of them.
        // "Overdue" ignores the project-end placeholder date given to items created without a due date.
        public async Task<List<TeamMemberTaskCountsResult>> GetTeamMemberTaskCountsAsync(DateTime now, IReadOnlyCollection<int>? projectIds = null)
        {
            const string sql = @"
                SELECT
                    u.""Name"" AS ""Name"",
                    u.""Position"" AS ""Position"",
                    COUNT(t.""Id"") AS ""AssignedTasks"",
                    COUNT(t.""Id"") FILTER (WHERE t.""Status"" = 'completed') AS ""CompletedTasks"",
                    COUNT(t.""Id"") FILTER (WHERE t.""Status"" != 'completed' AND t.""HasExplicitDueDate"" AND t.""DueDate"" < @now) AS ""OverdueTasks""
                FROM ""Users"" u
                LEFT JOIN ""Tasks"" t ON t.""AssignedToId"" = u.""Id"" AND (NOT @scoped OR t.""ProjectId"" = ANY(@ids))
                WHERE u.""IsActive"" = true
                  AND (NOT @scoped OR EXISTS (SELECT 1 FROM ""ProjectUsers"" pu WHERE pu.""UserId"" = u.""Id"" AND pu.""ProjectId"" = ANY(@ids)))
                GROUP BY u.""Id"", u.""Name"", u.""Position""
                ORDER BY u.""Id""";

            var connection = await GetOpenConnectionAsync();
            var args = new DynamicParameters(ScopeArgs(projectIds));
            args.Add("now", now);
            var result = await connection.QueryAsync<TeamMemberTaskCountsResult>(sql, args);
            return result.AsList();
        }

        private static object ScopeArgs(IReadOnlyCollection<int>? projectIds) => new
        {
            scoped = projectIds != null,
            ids = projectIds?.ToArray() ?? Array.Empty<int>(),
        };

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
