using KhoiProjectManagement.Application;
using Microsoft.Extensions.Logging;
using Quartz;

namespace KhoiProjectManagement.Quartz
{
    // Daily snapshot so the dashboard's KPI cards can show a trend delta against "7 days ago" -
    // see DashboardService.GetDashboardStatisticsAsync, which reads these back.
    public class DashboardSnapshotJob : IJob
    {
        private readonly IDashboardService _dashboardService;
        private readonly ILogger<DashboardSnapshotJob> _logger;

        public DashboardSnapshotJob(IDashboardService dashboardService, ILogger<DashboardSnapshotJob> logger)
        {
            _dashboardService = dashboardService;
            _logger = logger;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            try
            {
                await _dashboardService.CaptureSnapshotAsync();
                _logger.LogInformation("Dashboard stats snapshot captured at {Time}", DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error capturing dashboard stats snapshot");
            }
        }
    }
}
