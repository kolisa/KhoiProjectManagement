using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Application;
using KhoiProjectManagement.Domain;
using Microsoft.EntityFrameworkCore;

namespace KhoiProjectManagement.Application
{
    public class DashboardService : IDashboardService
    {
        private readonly IDashboardStatsRepository _statsRepo;
        private readonly IRepository<ProjectTask> _taskRepo;
        private readonly IRepository<DashboardStatsSnapshot> _snapshotRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IProjectScope _scope;

        public DashboardService(
            IDashboardStatsRepository statsRepo,
            IRepository<ProjectTask> taskRepo,
            IRepository<DashboardStatsSnapshot> snapshotRepo,
            IUnitOfWork unitOfWork,
            IProjectScope scope)
        {
            _scope = scope;
            _statsRepo = statsRepo;
            _taskRepo = taskRepo;
            _snapshotRepo = snapshotRepo;
            _unitOfWork = unitOfWork;
        }

        public async Task<DashboardStatisticsDto> GetDashboardStatisticsAsync()
        {
            // Need-to-know: a person's dashboard counts only their own projects (an administrator sees the whole company).
            var visibleProjects = await _scope.VisibleProjectIdsAsync();
            var counts = await _statsRepo.GetCountsAsync(DateTime.Now, visibleProjects);

            var totalProjects = counts.TotalProjects;
            var activeProjects = counts.ActiveProjects;
            var totalTasks = counts.TotalTasks;
            var completedTasks = counts.CompletedTasks;
            var inProgressTasks = counts.InProgressTasks;
            var todoTasks = counts.TodoTasks;
            var blockedTasks = counts.BlockedTasks;
            var overdueTasks = counts.OverdueTasks;
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
                BlockedTasks = blockedTasks,
                OverdueTasks = overdueTasks,
                CompletionRate = completionRate,
                // The stored baselines are company-wide, so they only mean something against company-wide counts.
                ActiveProjectsDelta = baseline == null || visibleProjects != null ? null : activeProjects - baseline.ActiveProjects,
                TotalTasksDelta = baseline == null || visibleProjects != null ? null : totalTasks - baseline.TotalTasks,
                OverdueTasksDelta = baseline == null || visibleProjects != null ? null : overdueTasks - baseline.OverdueTasks,
                CompletionRateDelta = baseline == null || visibleProjects != null ? null : completionRate - baseline.CompletionRate
            };
        }

        public async Task<int[]> GetWeeklyCompletionAsync()
        {
            var today = DateTime.UtcNow.Date;
            var daysSinceMonday = ((int)today.DayOfWeek + 6) % 7;
            var monday = today.AddDays(-daysSinceMonday);
            var nextMonday = monday.AddDays(7);

            var completedThisWeek = await (await _scope.ScopeAsync(_taskRepo.Query()))
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
