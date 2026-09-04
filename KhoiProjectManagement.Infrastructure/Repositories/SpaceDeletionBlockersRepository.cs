using System.Data;
using Dapper;
using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace KhoiProjectManagement.Infrastructure.Repositories
{
    public class SpaceDeletionBlockersRepository : ISpaceDeletionBlockersRepository
    {
        private readonly ProjectManagementContext _context;

        public SpaceDeletionBlockersRepository(ProjectManagementContext context)
        {
            _context = context;
        }

        public async Task<bool> HasBlockingChildrenAsync(int spaceId)
        {
            const string sql = @"
                SELECT
                    EXISTS(SELECT 1 FROM ""Spaces"" WHERE ""ParentSpaceId"" = @spaceId)
                    OR EXISTS(SELECT 1 FROM ""VaultEntries"" WHERE ""SpaceId"" = @spaceId)
                    OR EXISTS(SELECT 1 FROM ""WikiPages"" WHERE ""SpaceId"" = @spaceId)
                    OR EXISTS(SELECT 1 FROM ""LibraryFiles"" WHERE ""SpaceId"" = @spaceId)";

            var connection = _context.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync();
            }
            return await connection.QuerySingleAsync<bool>(sql, new { spaceId });
        }
    }
}
