using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Application;
using KhoiProjectManagement.Domain;
using Microsoft.EntityFrameworkCore;

namespace KhoiProjectManagement.Application
{
    public class ReportService : IReportService
    {
        private readonly IRepository<Project> _projectRepo;
        private readonly IRepository<User> _userRepo;
        private readonly IRepository<ProjectTask> _taskRepo;

        public ReportService(IRepository<Project> projectRepo, IRepository<User> userRepo, IRepository<ProjectTask> taskRepo)
        {
            _projectRepo = projectRepo;
            _userRepo = userRepo;
            _taskRepo = taskRepo;
        }

        public async Task<ProjectSummaryReportDto> GenerateProjectSummaryReportAsync()
        {
            var projects = await _projectRepo.Query()
                .Include(p => p.Tasks)
                .ToListAsync();

            var projectSummaries = projects.Select(p => new ProjectSummaryItemDto
            {
                Name = p.Name,
                Status = p.Status,
                TasksCount = p.Tasks.Count,
                CompletedTasks = p.Tasks.Count(t => t.Status == "completed"),
                CompletionRate = p.Tasks.Count == 0 ? 0 : (double)p.Tasks.Count(t => t.Status == "completed") / p.Tasks.Count * 100
            }).ToList();

            return new ProjectSummaryReportDto
            {
                GeneratedAt = DateTime.UtcNow,
                TotalProjects = projects.Count,
                ActiveProjects = projects.Count(p => p.Status == "active"),
                OverallCompletionRate = projects.SelectMany(p => p.Tasks).Count() == 0 ? 0 :
                    (double)projects.SelectMany(p => p.Tasks).Count(t => t.Status == "completed") /
                    projects.SelectMany(p => p.Tasks).Count() * 100,
                Projects = projectSummaries
            };
        }

        public async Task<TeamPerformanceReportDto> GenerateTeamPerformanceReportAsync()
        {
            var users = await _userRepo.Query()
                .Include(u => u.AssignedTasks)
                .Where(u => u.IsActive)
                .ToListAsync();

            var teamPerformance = users.Select(u => new TeamMemberPerformanceDto
            {
                Name = u.Name,
                Position = u.Position,
                AssignedTasks = u.AssignedTasks.Count,
                CompletedTasks = u.AssignedTasks.Count(t => t.Status == "completed"),
                OverdueTasks = u.AssignedTasks.Count(t => t.IsOverdue),
                CompletionRate = u.AssignedTasks.Count == 0 ? 0 :
                    (double)u.AssignedTasks.Count(t => t.Status == "completed") / u.AssignedTasks.Count * 100
            }).ToList();

            return new TeamPerformanceReportDto
            {
                GeneratedAt = DateTime.UtcNow,
                TeamMembers = teamPerformance
            };
        }

        public async Task<OverdueTasksReportDto> GenerateOverdueTasksReportAsync()
        {
            var overdueTasks = await _taskRepo.Query()
                .Include(t => t.Project)
                .Include(t => t.AssignedTo)
                .Where(t => t.Status != "completed" && t.DueDate < DateTime.UtcNow)
                .OrderBy(t => t.DueDate)
                .ToListAsync();

            var overdueTaskItems = overdueTasks.Select(t => new OverdueTaskItemDto
            {
                Title = t.Title,
                Project = t.Project?.Name ?? "Unknown Project",
                AssignedTo = t.AssignedTo?.Name ?? "Unassigned",
                DueDate = t.DueDate,
                DaysOverdue = (int)(DateTime.UtcNow - t.DueDate).TotalDays,
                Priority = t.Priority
            }).ToList();

            return new OverdueTasksReportDto
            {
                GeneratedAt = DateTime.UtcNow,
                TotalOverdueTasks = overdueTasks.Count,
                Tasks = overdueTaskItems
            };
        }
    }
}
