using KhoiProjectManagement.Application;
using Microsoft.Extensions.Logging;
using Quartz;

namespace KhoiProjectManagement.Quartz
{
    // Daily - dedup is by calendar day (see NotificationService.CheckBirthdaysAsync), so running more
    // than once on the same day is already a safe no-op.
    public class BirthdayCheckJob : IJob
    {
        private readonly INotificationService _notificationService;
        private readonly ILogger<BirthdayCheckJob> _logger;

        public BirthdayCheckJob(INotificationService notificationService, ILogger<BirthdayCheckJob> logger)
        {
            _notificationService = notificationService;
            _logger = logger;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            try
            {
                await _notificationService.CheckBirthdaysAsync();
                _logger.LogInformation("Birthdays checked at {Time}", DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking birthdays");
            }
        }
    }
}
