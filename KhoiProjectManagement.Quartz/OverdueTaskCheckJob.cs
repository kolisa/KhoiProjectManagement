using KhoiProjectManagement.Application;
using Microsoft.Extensions.Logging;
using Quartz;

namespace KhoiProjectManagement.Quartz
{
    // Moved from KhoiProjectManagementApi/BackgroundServices/OverdueTaskCheckerService.cs - Quartz's
    // DI-integrated job factory creates a scope per execution automatically, so this needs no manual
    // IServiceProvider.CreateScope() the way the old BackgroundService did.
    public class OverdueTaskCheckJob : IJob
    {
        private readonly INotificationService _notificationService;
        private readonly ILogger<OverdueTaskCheckJob> _logger;

        public OverdueTaskCheckJob(INotificationService notificationService, ILogger<OverdueTaskCheckJob> logger)
        {
            _notificationService = notificationService;
            _logger = logger;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            try
            {
                await _notificationService.CheckOverdueTasksAsync();
                _logger.LogInformation("Overdue tasks checked at {Time}", DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking overdue tasks");
            }
        }
    }
}
