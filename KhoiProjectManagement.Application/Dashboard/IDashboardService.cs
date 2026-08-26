using KhoiProjectManagement.Application;

namespace KhoiProjectManagement.Application
{
    public interface IDashboardService
    {
        Task<DashboardStatisticsDto> GetDashboardStatisticsAsync();

        // Mon-Sun task-completion counts for the current week (index 0 = Monday), by CompletedAt.
        Task<int[]> GetWeeklyCompletionAsync();

        // Called daily by DashboardSnapshotJob - captures today's stats for future trend deltas.
        Task CaptureSnapshotAsync();
    }
}
