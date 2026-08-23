using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Application;
using KhoiProjectManagement.Domain;
using Microsoft.EntityFrameworkCore;

namespace KhoiProjectManagement.Application
{
    public class DashboardService : IDashboardService
    {
        private readonly IRepository<Project> _projectRepo;
        private readonly IRepository<ProjectTask> _taskRepo;

        public DashboardService(IRepository<Project> projectRepo, IRepository<ProjectTask> taskRepo)
        {
            _projectRepo = projectRepo;
            _taskRepo = taskRepo;
        }

        public async Task<DashboardStatisticsDto> GetDashboardStatisticsAsync()
        {
            var projects = await _projectRepo.Query().ToListAsync();
            var tasks = await _taskRepo.Query().ToListAsync();

            var totalProjects = projects.Count;
            var activeProjects = projects.Count(p => p.Status == "active");
            var totalTasks = tasks.Count;
            var completedTasks = tasks.Count(t => t.Status == "completed");
            var inProgressTasks = tasks.Count(t => t.Status == "in-progress");
            var todoTasks = tasks.Count(t => t.Status == "todo");
            var overdueTasks = tasks.Count(t => t.IsOverdue);

            return new DashboardStatisticsDto
            {
                TotalProjects = totalProjects,
                ActiveProjects = activeProjects,
                TotalTasks = totalTasks,
                CompletedTasks = completedTasks,
                InProgressTasks = inProgressTasks,
                TodoTasks = todoTasks,
                OverdueTasks = overdueTasks,
                CompletionRate = totalTasks == 0 ? 0 : (double)completedTasks / totalTasks * 100
            };
        }
    }
}
