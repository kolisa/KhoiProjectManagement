using KhoiProjectManagement.Application;
using Microsoft.Extensions.Logging;
using Quartz;

namespace KhoiProjectManagement.Quartz
{
    // Checked daily like LoginReminderCheckJob/DormantUserCheckJob - the real weekly cadence is
    // enforced by NotificationService.GenerateWeeklyDigestsAsync's dedup window
    // (Notifications:WeeklyDigestRepeatDays), not by this job's own trigger interval.
    public class WeeklyDigestJob : IJob
    {
        private readonly INotificationService _notificationService;
        private readonly ILogger<WeeklyDigestJob> _logger;

        public WeeklyDigestJob(INotificationService notificationService, ILogger<WeeklyDigestJob> logger)
        {
            _notificationService = notificationService;
            _logger = logger;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            try
            {
                await _notificationService.GenerateWeeklyDigestsAsync();
                _logger.LogInformation("Weekly digests generated at {Time}", DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating weekly digests");
            }
        }
    }
}
