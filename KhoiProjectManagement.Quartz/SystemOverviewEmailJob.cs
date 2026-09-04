using KhoiProjectManagement.Application;
using Microsoft.Extensions.Logging;
using Quartz;

namespace KhoiProjectManagement.Quartz
{
    // Fires every Friday 10am (see Program.cs's cron trigger, the one genuinely calendar-based
    // trigger in this project - every other job here uses an hourly/daily WithSimpleSchedule with the
    // real cadence enforced by a dedup check inside the service instead). A standing "what this
    // system is and how to use it" tour sent to every active user, not tied to any per-user activity.
    public class SystemOverviewEmailJob : IJob
    {
        private readonly IUserService _userService;
        private readonly IEmailService _emailService;
        private readonly ILogger<SystemOverviewEmailJob> _logger;

        public SystemOverviewEmailJob(IUserService userService, IEmailService emailService, ILogger<SystemOverviewEmailJob> logger)
        {
            _userService = userService;
            _emailService = emailService;
            _logger = logger;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            try
            {
                var users = await _userService.GetAllUsersAsync();
                var count = 0;
                foreach (var user in users)
                {
                    await _emailService.SendSystemOverviewEmailAsync(user.Email, user.Name);
                    count++;
                }
                _logger.LogInformation("System overview email queued for {Count} users at {Time}", count, DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending system overview emails");
            }
        }
    }
}
