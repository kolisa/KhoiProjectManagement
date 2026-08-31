using KhoiProjectManagement.Application;
using Microsoft.Extensions.Logging;
using Quartz;

namespace KhoiProjectManagement.Quartz
{
    // Daily, like LoginReminderCheckJob - covers the distinct population of users who finished
    // onboarding but stopped logging in (see NotificationService.CheckDormantUsersAsync).
    public class DormantUserCheckJob : IJob
    {
        private readonly INotificationService _notificationService;
        private readonly ILogger<DormantUserCheckJob> _logger;

        public DormantUserCheckJob(INotificationService notificationService, ILogger<DormantUserCheckJob> logger)
        {
            _notificationService = notificationService;
            _logger = logger;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            try
            {
                await _notificationService.CheckDormantUsersAsync();
                _logger.LogInformation("Dormant users checked at {Time}", DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking dormant users");
            }
        }
    }
}
