using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using Microsoft.EntityFrameworkCore;

namespace KhoiProjectManagement.Application
{
    // Projects/Tasks are already org-wide visible with no per-item ownership filter (see
    // ProjectsController.GetProjects/TasksController.GetTasks) - this mirrors that, so search never
    // surfaces anything the caller couldn't already find by browsing those tabs directly.
    public class GlobalSearchService : IGlobalSearchService
    {
        private const int MaxResultsPerCategory = 5;
        private const int MinQueryLength = 2;

        private readonly IRepository<Project> _projectRepo;
        private readonly IRepository<ProjectTask> _taskRepo;
        private readonly IRepository<User> _userRepo;

        public GlobalSearchService(IRepository<Project> projectRepo, IRepository<ProjectTask> taskRepo, IRepository<User> userRepo)
        {
            _projectRepo = projectRepo;
            _taskRepo = taskRepo;
            _userRepo = userRepo;
        }

        public async Task<GlobalSearchResultDto> SearchAsync(string query)
        {
            var result = new GlobalSearchResultDto();
            var trimmed = query?.Trim() ?? string.Empty;
            if (trimmed.Length < MinQueryLength)
                return result;

            var lowered = trimmed.ToLower();

            result.Projects = await _projectRepo.Query()
                .Where(p => p.Name.ToLower().Contains(lowered))
                .OrderBy(p => p.Name)
                .Take(MaxResultsPerCategory)
                .Select(p => new GlobalSearchItemDto { Id = p.Id, Title = p.Name, Subtitle = p.Status })
                .ToListAsync();

            result.Tasks = await _taskRepo.Query()
                .Where(t => t.Title.ToLower().Contains(lowered))
                .OrderBy(t => t.Title)
                .Take(MaxResultsPerCategory)
                .Select(t => new GlobalSearchItemDto { Id = t.Id, Title = t.Title, Subtitle = t.Status })
                .ToListAsync();

            result.People = await _userRepo.Query()
                .Where(u => u.IsActive && (u.Name.ToLower().Contains(lowered) || u.Email.ToLower().Contains(lowered)))
                .OrderBy(u => u.Name)
                .Take(MaxResultsPerCategory)
                .Select(u => new GlobalSearchItemDto { Id = u.Id, Title = u.Name, Subtitle = u.Position })
                .ToListAsync();

            return result;
        }
    }
}
