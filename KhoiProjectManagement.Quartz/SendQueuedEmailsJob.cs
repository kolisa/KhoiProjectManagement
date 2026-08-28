using KhoiProjectManagement.Application;
using Microsoft.Extensions.Logging;
using Quartz;

namespace KhoiProjectManagement.Quartz
{
    // Every EmailService.Send*EmailAsync call (except the scheduled-report one, which still sends
    // synchronously - see EmailService) now just inserts a Pending EmailLog row and returns, so HTTP
    // requests that used to wait on a full SMTP round trip return immediately. This job is the actual
    // delivery mechanism - runs every 15s (see Program.cs), far more often than the other jobs' hourly/
    // daily cadence, since a queued email's real-world latency is bounded by this interval.
    public class SendQueuedEmailsJob : IJob
    {
        private readonly IEmailService _emailService;
        private readonly ILogger<SendQueuedEmailsJob> _logger;

        public SendQueuedEmailsJob(IEmailService emailService, ILogger<SendQueuedEmailsJob> logger)
        {
            _emailService = emailService;
            _logger = logger;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            try
            {
                await _emailService.DispatchPendingEmailsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error dispatching queued emails");
            }
        }
    }
}
