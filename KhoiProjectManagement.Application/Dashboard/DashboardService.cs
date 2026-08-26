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
        private readonly IRepository<DashboardStatsSnapshot> _snapshotRepo;
        private readonly IUnitOfWork _unitOfWork;

        public DashboardService(
            IRepository<Project> projectRepo,
            IRepository<ProjectTask> taskRepo,
            IRepository<DashboardStatsSnapshot> snapshotRepo,
            IUnitOfWork unitOfWork)
        {
            _projectRepo = projectRepo;
            _taskRepo = taskRepo;
            _snapshotRepo = snapshotRepo;
            _unitOfWork = unitOfWork;
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
            var completionRate = totalTasks == 0 ? 0 : (double)completedTasks / totalTasks * 100;

            var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);
            var baseline = await _snapshotRepo.Query()
                .Where(s => s.CapturedAt <= sevenDaysAgo)
                .OrderByDescending(s => s.CapturedAt)
                .FirstOrDefaultAsync();

            return new DashboardStatisticsDto
            {
                TotalProjects = totalProjects,
                ActiveProjects = activeProjects,
                TotalTasks = totalTasks,
                CompletedTasks = completedTasks,
                InProgressTasks = inProgressTasks,
                TodoTasks = todoTasks,
                OverdueTasks = overdueTasks,
                CompletionRate = completionRate,
                ActiveProjectsDelta = baseline == null ? null : activeProjects - baseline.ActiveProjects,
                TotalTasksDelta = baseline == null ? null : totalTasks - baseline.TotalTasks,
                OverdueTasksDelta = baseline == null ? null : overdueTasks - baseline.OverdueTasks,
                CompletionRateDelta = baseline == null ? null : completionRate - baseline.CompletionRate
            };
        }

        public async Task<int[]> GetWeeklyCompletionAsync()
        {
            var today = DateTime.UtcNow.Date;
            var daysSinceMonday = ((int)today.DayOfWeek + 6) % 7;
            var monday = today.AddDays(-daysSinceMonday);
            var nextMonday = monday.AddDays(7);

            var completedThisWeek = await _taskRepo.Query()
                .Where(t => t.CompletedAt != null && t.CompletedAt >= monday && t.CompletedAt < nextMonday)
                .Select(t => t.CompletedAt!.Value)
                .ToListAsync();

            var counts = new int[7];
            foreach (var completedAt in completedThisWeek)
            {
                var dayIndex = (int)(completedAt.Date - monday).TotalDays;
                if (dayIndex >= 0 && dayIndex < 7)
                    counts[dayIndex]++;
            }

            return counts;
        }

        public async Task CaptureSnapshotAsync()
        {
            var stats = await GetDashboardStatisticsAsync();

            _snapshotRepo.Add(new DashboardStatsSnapshot
            {
                CapturedAt = DateTime.UtcNow,
                TotalProjects = stats.TotalProjects,
                ActiveProjects = stats.ActiveProjects,
                TotalTasks = stats.TotalTasks,
                CompletedTasks = stats.CompletedTasks,
                InProgressTasks = stats.InProgressTasks,
                TodoTasks = stats.TodoTasks,
                OverdueTasks = stats.OverdueTasks,
                CompletionRate = stats.CompletionRate
            });

            await _unitOfWork.SaveChangesAsync();
        }
    }
}
