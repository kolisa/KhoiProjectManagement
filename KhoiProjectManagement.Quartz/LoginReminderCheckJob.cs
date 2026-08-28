using KhoiProjectManagement.Application;
using Microsoft.Extensions.Logging;
using Quartz;

namespace KhoiProjectManagement.Quartz
{
    // Daily, not hourly - unlike overdue tasks/due reminders, "hasn't finished account setup" doesn't
    // need frequent checking (see NotificationService.CheckInactiveUsersAsync's dedup window).
    public class LoginReminderCheckJob : IJob
    {
        private readonly INotificationService _notificationService;
        private readonly ILogger<LoginReminderCheckJob> _logger;

        public LoginReminderCheckJob(INotificationService notificationService, ILogger<LoginReminderCheckJob> logger)
        {
            _notificationService = notificationService;
            _logger = logger;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            try
            {
                await _notificationService.CheckInactiveUsersAsync();
                _logger.LogInformation("Inactive users checked at {Time}", DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking inactive users");
            }
        }
    }
}
