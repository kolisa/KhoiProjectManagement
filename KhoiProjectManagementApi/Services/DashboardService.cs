using KhoiProjectManagement.Models.DTOs;
using KhoiProjectManagementApi.Data;
using Microsoft.EntityFrameworkCore;

namespace KhoiProjectManagementApi.Services
{
    public class DashboardService : IDashboardService
    {
        private readonly ProjectManagementContext _context;

        public DashboardService(ProjectManagementContext context)
        {
            _context = context;
        }

        public async Task<DashboardStatisticsDto> GetDashboardStatisticsAsync()
        {
            var projects = await _context.Projects.ToListAsync();
            var tasks = await _context.Tasks.ToListAsync();

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
