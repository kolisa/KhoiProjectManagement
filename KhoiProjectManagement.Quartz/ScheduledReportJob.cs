using KhoiProjectManagement.Application;
using Microsoft.Extensions.Logging;
using Quartz;

namespace KhoiProjectManagement.Quartz
{
    // Hourly check for due ScheduledReport rows - same cadence as OverdueTaskCheckJob/ReminderDueCheckJob,
    // since a schedule is weekly-granularity anyway and an hourly poll is cheap.
    public class ScheduledReportJob : IJob
    {
        private readonly IReportScheduleService _reportScheduleService;
        private readonly ILogger<ScheduledReportJob> _logger;

        public ScheduledReportJob(IReportScheduleService reportScheduleService, ILogger<ScheduledReportJob> logger)
        {
            _reportScheduleService = reportScheduleService;
            _logger = logger;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            try
            {
                await _reportScheduleService.RunDueSchedulesAsync();
                _logger.LogInformation("Scheduled report check completed at {Time}", DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running scheduled reports");
            }
        }
    }
}
