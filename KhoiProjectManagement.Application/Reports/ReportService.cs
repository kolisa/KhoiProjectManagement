using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Application;
using KhoiProjectManagement.Domain;
using Microsoft.EntityFrameworkCore;

namespace KhoiProjectManagement.Application
{
    public class ReportService : IReportService
    {
        private readonly IReportStatsRepository _statsRepo;
        private readonly IRepository<ProjectTask> _taskRepo;

        public ReportService(IReportStatsRepository statsRepo, IRepository<ProjectTask> taskRepo)
        {
            _statsRepo = statsRepo;
            _taskRepo = taskRepo;
        }

        public async Task<ProjectSummaryReportDto> GenerateProjectSummaryReportAsync()
        {
            var projectCounts = await _statsRepo.GetProjectTaskCountsAsync();

            var projectSummaries = projectCounts.Select(p => new ProjectSummaryItemDto
            {
                Name = p.Name,
                Status = p.Status,
                TasksCount = p.TasksCount,
                CompletedTasks = p.CompletedTasks,
                CompletionRate = p.TasksCount == 0 ? 0 : (double)p.CompletedTasks / p.TasksCount * 100
            }).ToList();

            var totalTasks = projectCounts.Sum(p => p.TasksCount);
            var totalCompletedTasks = projectCounts.Sum(p => p.CompletedTasks);

            return new ProjectSummaryReportDto
            {
                GeneratedAt = DateTime.UtcNow,
                TotalProjects = projectCounts.Count,
                ActiveProjects = projectCounts.Count(p => p.Status == "active"),
                OverallCompletionRate = totalTasks == 0 ? 0 : (double)totalCompletedTasks / totalTasks * 100,
                Projects = projectSummaries
            };
        }

        public async Task<TeamPerformanceReportDto> GenerateTeamPerformanceReportAsync()
        {
            var memberCounts = await _statsRepo.GetTeamMemberTaskCountsAsync(DateTime.Now);

            var teamPerformance = memberCounts.Select(u => new TeamMemberPerformanceDto
            {
                Name = u.Name,
                Position = u.Position,
                AssignedTasks = u.AssignedTasks,
                CompletedTasks = u.CompletedTasks,
                OverdueTasks = u.OverdueTasks,
                CompletionRate = u.AssignedTasks == 0 ? 0 : (double)u.CompletedTasks / u.AssignedTasks * 100
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
