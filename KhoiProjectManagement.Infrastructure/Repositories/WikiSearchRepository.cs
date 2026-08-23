using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using KhoiProjectManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace KhoiProjectManagement.Infrastructure.Repositories
{
    public class WikiSearchRepository : IWikiSearchRepository
    {
        private readonly ProjectManagementContext _context;

        public WikiSearchRepository(ProjectManagementContext context)
        {
            _context = context;
        }

        public async Task<List<WikiPage>> FindCandidatesAsync(string query, int take)
        {
            return await _context.WikiPages
                .Include(p => p.Space)
                .Where(p => p.IsActive)
                .Where(p => EF.Functions.ToTsVector("english", (p.Title ?? "") + " " + (p.CurrentContentMarkdown ?? ""))
                    .Matches(EF.Functions.PlainToTsQuery("english", query)))
                .OrderByDescending(p => EF.Functions.ToTsVector("english", (p.Title ?? "") + " " + (p.CurrentContentMarkdown ?? ""))
                    .Rank(EF.Functions.PlainToTsQuery("english", query)))
                .Take(take)
                .ToListAsync();
        }
    }
}
