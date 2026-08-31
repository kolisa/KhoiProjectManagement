using KhoiProjectManagement.Application;
using Microsoft.Extensions.Logging;
using Quartz;

namespace KhoiProjectManagement.Quartz
{
    // Daily - "never uploaded anything" doesn't need frequent checking (see
    // NotificationService.CheckUsersWithNoDocumentsAsync's dedup window).
    public class NoDocumentsNudgeJob : IJob
    {
        private readonly INotificationService _notificationService;
        private readonly ILogger<NoDocumentsNudgeJob> _logger;

        public NoDocumentsNudgeJob(INotificationService notificationService, ILogger<NoDocumentsNudgeJob> logger)
        {
            _notificationService = notificationService;
            _logger = logger;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            try
            {
                await _notificationService.CheckUsersWithNoDocumentsAsync();
                _logger.LogInformation("No-documents nudge checked at {Time}", DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking users with no documents");
            }
        }
    }
}
