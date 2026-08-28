using KhoiProjectManagement.Application;
using KhoiProjectManagement.Domain;
using KhoiProjectManagement.Infrastructure.Data;
using MailKit.Net.Smtp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;


namespace KhoiProjectManagement.Infrastructure.Services
{
    public class EmailService : IEmailService
    {
        // Capped per DispatchPendingEmailsAsync run (called every 15s by SendQueuedEmailsJob) so a
        // sudden burst can't make one run take unboundedly long - the next run picks up the rest.
        private const int MaxDispatchBatchSize = 50;

        private readonly IConfiguration _configuration;
        private readonly ProjectManagementContext _context;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ProjectManagementContext context, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _context = context;
            _logger = logger;
        }

        private string GetFrontendUrl(string path = "")
        {
            var frontendBaseUrl = (_configuration["App:FrontendBaseUrl"] ?? "http://localhost:3000").TrimEnd('/');
            return path.Length == 0 ? frontendBaseUrl + "/" : $"{frontendBaseUrl}/{path.TrimStart('/')}";
        }

        public async Task SendTaskAssignmentEmailAsync(string toEmail, string taskTitle, string projectName)
        {
            var subject = $"Task Assignment: {taskTitle}";
            var inner = $@"
                <p>You have been assigned to a new task:</p>
                <p><strong>Task:</strong> {taskTitle}</p>
                <p><strong>Project:</strong> {projectName}</p>
            ";
            var body = EmailTemplates.Wrap("New Task Assignment", inner, "View Task", GetFrontendUrl("?tab=tasks"), GetFrontendUrl());

            await EnqueueEmailAsync(toEmail, subject, body, "task_assignment");
        }

        public async Task SendOverdueTaskEmailAsync(string toEmail, string taskTitle, DateTime dueDate)
        {
            var subject = $"Overdue Task: {taskTitle}";
            var inner = $@"
                <p>The following task is overdue and requires your attention:</p>
                <p><strong>Task:</strong> {taskTitle}</p>
                <p><strong>Due Date:</strong> {dueDate:yyyy-MM-dd}</p>
                <p><strong>Days Overdue:</strong> {(DateTime.Now - dueDate).Days}</p>
                <p>Please update the task status as soon as possible.</p>
            ";
            var body = EmailTemplates.Wrap("Task Overdue Notification", inner, "View Task", GetFrontendUrl("?tab=tasks"), GetFrontendUrl());

            await EnqueueEmailAsync(toEmail, subject, body, "overdue_reminder");
        }

        public async Task SendReminderDueEmailAsync(string toEmail, string reminderTitle, DateTime dueAt)
        {
            var subject = $"Reminder: {reminderTitle}";
            var inner = $@"
                <p>A reminder you're assigned to has reached its due time:</p>
                <p><strong>{reminderTitle}</strong></p>
                <p><strong>Due:</strong> {dueAt:yyyy-MM-dd HH:mm}</p>
            ";
            var body = EmailTemplates.Wrap("Reminder Due", inner, "View Reminders", GetFrontendUrl("?tab=reminders"), GetFrontendUrl());

            await EnqueueEmailAsync(toEmail, subject, body, "reminder_due");
        }

        public async Task SendProjectCreatedEmailAsync(string toEmail, string projectName)
        {
            var subject = $"Project Created: {projectName}";
            var inner = $@"
                <p>A new project has been created and you have been added as a team member:</p>
                <p><strong>Project:</strong> {projectName}</p>
            ";
            var body = EmailTemplates.Wrap("New Project Created", inner, "View Project", GetFrontendUrl("?tab=projects"), GetFrontendUrl());

            await EnqueueEmailAsync(toEmail, subject, body, "project_created");
        }

        public async Task SendMentionEmailAsync(string toEmail, string mentionedByName, string contextLabel, string contextTitle, string commentBody, string? contextUrl = null)
        {
            var subject = $"{mentionedByName} mentioned you in a comment";
            // commentBody is free-form user input embedded in an HTML email - encode it, unlike the
            // other templates here which only ever interpolate system-controlled strings (titles/names).
            var encodedBody = System.Net.WebUtility.HtmlEncode(commentBody);
            var inner = $@"
                <p><strong>{mentionedByName}</strong> mentioned you in a comment on {contextLabel} <strong>{contextTitle}</strong>:</p>
                <blockquote style=""border-left: 3px solid #ccc; margin: 0; padding-left: 1em; color: #555;"">{encodedBody}</blockquote>
            ";
            var body = EmailTemplates.Wrap("You were mentioned in a comment", inner, "View", contextUrl ?? GetFrontendUrl(), GetFrontendUrl());

            await EnqueueEmailAsync(toEmail, subject, body, "mention");
        }

        public async Task SendPasswordResetEmailAsync(string toEmail, string userName, string resetLink)
        {
            var subject = "Reset your Khoi Pro password";
            var inner = $@"
                <p>Hi {userName},</p>
                <p>We received a request to reset your Khoi Pro password. Click the button below to choose a new one. This link expires in 1 hour.</p>
                <p>If you didn't request this, you can safely ignore this email - your password won't be changed.</p>
            ";
            var body = EmailTemplates.Wrap("Reset Your Password", inner, "Reset Password", resetLink, GetFrontendUrl());

            await EnqueueEmailAsync(toEmail, subject, body, "password_reset");
        }

        public async Task SendTemporaryPasswordEmailAsync(string toEmail, string userName, string tempPassword)
        {
            var subject = "Your Khoi Pro account";
            var inner = $@"
                <p>Hi {userName},</p>
                <p>An account has been created for you on Khoi Pro. Here's your temporary password:</p>
                <p style=""font-size: 18px; font-weight: 600; letter-spacing: 0.05em; background: #f3f4f6; padding: 10px 14px; border-radius: 8px; display: inline-block;"">{tempPassword}</p>
                <p>Log in with this password and you'll be asked to choose your own before you can continue.</p>
            ";
            var body = EmailTemplates.Wrap("Welcome to Khoi Pro", inner, "Log In", GetFrontendUrl(), GetFrontendUrl());

            await EnqueueEmailAsync(toEmail, subject, body, "temp_password");
        }

        public async Task SendLoginReminderEmailAsync(string toEmail, string userName, int daysSinceInvite)
        {
            var subject = "Finish setting up your Khoi Pro account";
            var inner = $@"
                <p>Hi {userName},</p>
                <p>Your Khoi Pro account was set up {daysSinceInvite} day{(daysSinceInvite == 1 ? "" : "s")} ago, but you haven't logged in yet to choose your own password.</p>
                <p>If you've lost your temporary password, use &ldquo;Forgot password&rdquo; on the login screen to get a new link.</p>
            ";
            var body = EmailTemplates.Wrap("Your account is waiting for you", inner, "Log In Now", GetFrontendUrl(), GetFrontendUrl());

            await EnqueueEmailAsync(toEmail, subject, body, "login_reminder");
        }

        public async Task SendScheduledReportEmailAsync(string toEmail, string reportTitle, byte[] attachmentContent, string attachmentFileName, string attachmentContentType)
        {
            // Not queued (see IEmailService's comment) - already runs off the request thread via
            // ScheduledReportJob, and the outbox table has no column for a byte[] attachment.
            var subject = $"Scheduled report: {reportTitle}";
            var inner = $@"
                <p>Your scheduled report is ready:</p>
                <p><strong>{reportTitle}</strong></p>
                <p>It's attached to this email.</p>
            ";
            var body = EmailTemplates.Wrap("Scheduled Report", inner, "View Reports", GetFrontendUrl("?tab=reports"), GetFrontendUrl());

            var log = new EmailLog
            {
                ToEmail = toEmail,
                Subject = subject,
                Body = body,
                EmailType = "scheduled_report",
                Status = EmailLogStatus.Pending,
                SentAt = DateTime.UtcNow
            };
            _context.EmailLogs.Add(log);
            await _context.SaveChangesAsync();

            await SendAndRecordAsync(log, (attachmentContent, attachmentFileName, attachmentContentType));
        }

        // Fast path: every queued email type ends up here - a single EF insert, no SMTP. The actual
        // send happens later, off the request thread, in DispatchPendingEmailsAsync.
        private async Task EnqueueEmailAsync(string toEmail, string subject, string htmlBody, string emailType)
        {
            _context.EmailLogs.Add(new EmailLog
            {
                ToEmail = toEmail,
                Subject = subject,
                Body = htmlBody,
                EmailType = emailType,
                Status = EmailLogStatus.Pending,
                SentAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
        }

        // Called only by SendQueuedEmailsJob (Quartz, every 15s) - never awaited by an HTTP request.
        // FIFO by design: Id is a monotonically-increasing identity column assigned at enqueue time, so
        // ordering by it processes emails in exactly the order they were queued - including across
        // multiple runs if a backlog ever exceeds one batch (each run always takes the oldest
        // MaxDispatchBatchSize still Pending, never skips ahead to newer ones).
        public async Task DispatchPendingEmailsAsync()
        {
            var pending = await _context.EmailLogs
                .Where(e => e.Status == EmailLogStatus.Pending)
                .OrderBy(e => e.Id)
                .Take(MaxDispatchBatchSize)
                .ToListAsync();

            foreach (var log in pending)
            {
                await SendAndRecordAsync(log, attachment: null);
            }
        }

        // Shared by the immediate-send path (scheduled reports) and the queued-dispatch path - builds
        // the MimeMessage from an EmailLog row's already-rendered Subject/Body and updates that same
        // row's Status/ErrorMessage in place (never inserts a second row).
        private async Task SendAndRecordAsync(EmailLog log, (byte[] Content, string FileName, string ContentType)? attachment)
        {
            try
            {
                var fromAddress = _configuration["Email:FromAddress"]
                    ?? throw new InvalidOperationException("Email:FromAddress is not configured.");
                var smtpHost = _configuration["Email:SmtpHost"]
                    ?? throw new InvalidOperationException("Email:SmtpHost is not configured.");

                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(_configuration["Email:FromName"], fromAddress));
                message.To.Add(new MailboxAddress("", log.ToEmail));
                message.Subject = log.Subject;

                var bodyBuilder = new BodyBuilder
                {
                    HtmlBody = log.Body
                };
                if (attachment.HasValue)
                {
                    bodyBuilder.Attachments.Add(attachment.Value.FileName, attachment.Value.Content, ContentType.Parse(attachment.Value.ContentType));
                }
                message.Body = bodyBuilder.ToMessageBody();

                using var client = new SmtpClient
                {
                    // MailKit's default per-operation socket timeout is 100 seconds - bounded to a much
                    // shorter, still-generous timeout instead, so a slow/unreachable relay can't stall a
                    // dispatch batch (or, for the still-synchronous scheduled-report path, the calling
                    // Quartz job) for minutes.
                    Timeout = 20_000
                };
                await client.ConnectAsync(
                    smtpHost,
                    int.Parse(_configuration["Email:SmtpPort"] ?? "587"),
                    MailKit.Security.SecureSocketOptions.StartTls
                );

                var username = _configuration["Email:SmtpUsername"];
                var password = _configuration["Email:SmtpPassword"];

                if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
                {
                    await client.AuthenticateAsync(username, password);
                }

                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                log.Status = EmailLogStatus.Sent;
                log.IsSuccess = true;
                log.ErrorMessage = null;
                await _context.SaveChangesAsync();
                _logger.LogInformation("Email sent: {EmailType} to {ToEmail}", log.EmailType, log.ToEmail);
            }
            catch (Exception ex)
            {
                log.Status = EmailLogStatus.Failed;
                log.IsSuccess = false;
                log.ErrorMessage = ex.Message;
                await _context.SaveChangesAsync();
                _logger.LogError(ex, "Failed to send email: {EmailType} to {ToEmail}", log.EmailType, log.ToEmail);
            }
        }
    }
}
