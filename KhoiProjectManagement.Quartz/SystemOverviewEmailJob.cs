using KhoiProjectManagement.Application;
using Microsoft.Extensions.Logging;
using Quartz;

namespace KhoiProjectManagement.Quartz
{
    // The one genuinely calendar-based trigger in this project (see Program.cs's cron trigger comment)
    // - every other job here uses an hourly/daily WithSimpleSchedule with the real cadence enforced
    // inside the service instead. Sends each fully-onboarded active user a personalized nudge toward
    // whatever part of KhoiHub they haven't tried yet, or a short highlights email if they've tried
    // everything tracked - see NotificationService.SendSystemOverviewEmailsAsync for the actual logic.
    // Matches every other job in this project (OverdueTaskCheckJob etc.): a thin wrapper delegating to
    // one NotificationService method, not IUserService/IEmailService directly.
    public class SystemOverviewEmailJob : IJob
    {
        private readonly INotificationService _notificationService;
        private readonly ILogger<SystemOverviewEmailJob> _logger;

        public SystemOverviewEmailJob(INotificationService notificationService, ILogger<SystemOverviewEmailJob> logger)
        {
            _notificationService = notificationService;
            _logger = logger;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            try
            {
                await _notificationService.SendSystemOverviewEmailsAsync();
                _logger.LogInformation("System overview emails sent at {Time}", DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending system overview emails");
            }
        }
    }
}
