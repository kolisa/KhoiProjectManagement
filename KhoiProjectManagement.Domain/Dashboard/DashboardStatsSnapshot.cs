namespace KhoiProjectManagement.Domain
{
    // One row per day, written by DashboardSnapshotJob. Trend deltas on the dashboard compare the
    // live DashboardStatisticsDto against the nearest snapshot >= 7 days old - if none exists yet
    // (e.g. the first week after this shipped), the delta is simply omitted rather than faked.
    public class DashboardStatsSnapshot : BaseEntity
    {
        public DateTime CapturedAt { get; set; } = DateTime.UtcNow;

        public int TotalProjects { get; set; }
        public int ActiveProjects { get; set; }
        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int InProgressTasks { get; set; }
        public int TodoTasks { get; set; }
        public int OverdueTasks { get; set; }
        public double CompletionRate { get; set; }
    }
}
