using KhoiProjectManagement.Application;
using Microsoft.Extensions.Logging;
using Quartz;

namespace KhoiProjectManagement.Quartz
{
    // Moved from KhoiProjectManagementApi/BackgroundServices/ReminderDueCheckerService.cs - mirrors
    // OverdueTaskCheckJob exactly.
    public class ReminderDueCheckJob : IJob
    {
        private readonly IReminderService _reminderService;
        private readonly ILogger<ReminderDueCheckJob> _logger;

        public ReminderDueCheckJob(IReminderService reminderService, ILogger<ReminderDueCheckJob> logger)
        {
            _reminderService = reminderService;
            _logger = logger;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            try
            {
                await _reminderService.CheckDueRemindersAsync();
                _logger.LogInformation("Due reminders checked at {Time}", DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking due reminders");
            }
        }
    }
}
