using KhoiProjectManagement.Application;
using KhoiProjectManagement.Domain;
using KhoiProjectManagement.Infrastructure.Data;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;


namespace KhoiProjectManagement.Infrastructure.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ProjectManagementContext _context;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ProjectManagementContext context, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _context = context;
            _logger = logger;
        }

        public async Task SendTaskAssignmentEmailAsync(string toEmail, string taskTitle, string projectName)
        {
            var subject = $"Task Assignment: {taskTitle}";
            var inner = $@"
                <p>You have been assigned to a new task:</p>
                <p><strong>Task:</strong> {taskTitle}</p>
                <p><strong>Project:</strong> {projectName}</p>
                <p>Please log in to Khoi Pro to view the details.</p>
            ";
            var body = EmailTemplates.Wrap("New Task Assignment", inner);

            await SendEmailAsync(toEmail, subject, body, "task_assignment");
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
            var body = EmailTemplates.Wrap("Task Overdue Notification", inner);

            await SendEmailAsync(toEmail, subject, body, "overdue_reminder");
        }

        public async Task SendReminderDueEmailAsync(string toEmail, string reminderTitle, DateTime dueAt)
        {
            var subject = $"Reminder: {reminderTitle}";
            var inner = $@"
                <p>A reminder you're assigned to has reached its due time:</p>
                <p><strong>{reminderTitle}</strong></p>
                <p><strong>Due:</strong> {dueAt:yyyy-MM-dd HH:mm}</p>
            ";
            var body = EmailTemplates.Wrap("Reminder Due", inner);

            await SendEmailAsync(toEmail, subject, body, "reminder_due");
        }

        public async Task SendProjectCreatedEmailAsync(string toEmail, string projectName)
        {
            var subject = $"Project Created: {projectName}";
            var inner = $@"
                <p>A new project has been created and you have been added as a team member:</p>
                <p><strong>Project:</strong> {projectName}</p>
                <p>Please log in to Khoi Pro to view the project details.</p>
            ";
            var body = EmailTemplates.Wrap("New Project Created", inner);

            await SendEmailAsync(toEmail, subject, body, "project_created");
        }

        public async Task SendMentionEmailAsync(string toEmail, string mentionedByName, string contextLabel, string contextTitle, string commentBody)
        {
            var subject = $"{mentionedByName} mentioned you in a comment";
            // commentBody is free-form user input embedded in an HTML email - encode it, unlike the
            // other templates here which only ever interpolate system-controlled strings (titles/names).
            var encodedBody = System.Net.WebUtility.HtmlEncode(commentBody);
            var inner = $@"
                <p><strong>{mentionedByName}</strong> mentioned you in a comment on {contextLabel} <strong>{contextTitle}</strong>:</p>
                <blockquote style=""border-left: 3px solid #ccc; margin: 0; padding-left: 1em; color: #555;"">{encodedBody}</blockquote>
                <p>Please log in to Khoi Pro to view and reply.</p>
            ";
            var body = EmailTemplates.Wrap("You were mentioned in a comment", inner);

            await SendEmailAsync(toEmail, subject, body, "mention");
        }

        public async Task SendPasswordResetEmailAsync(string toEmail, string userName, string resetLink)
        {
            var subject = "Reset your Khoi Pro password";
            var inner = $@"
                <p>Hi {userName},</p>
                <p>We received a request to reset your Khoi Pro password. Click the button below to choose a new one. This link expires in 1 hour.</p>
                <p>If you didn't request this, you can safely ignore this email - your password won't be changed.</p>
            ";
            var body = EmailTemplates.Wrap("Reset Your Password", inner, "Reset Password", resetLink);

            await SendEmailAsync(toEmail, subject, body, "password_reset");
        }

        private async Task SendEmailAsync(string toEmail, string subject, string htmlBody, string emailType)
        {
            try
            {
                var fromAddress = _configuration["Email:FromAddress"]
                    ?? throw new InvalidOperationException("Email:FromAddress is not configured.");
                var smtpHost = _configuration["Email:SmtpHost"]
                    ?? throw new InvalidOperationException("Email:SmtpHost is not configured.");

                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(_configuration["Email:FromName"], fromAddress));
                message.To.Add(new MailboxAddress("", toEmail));
                message.Subject = subject;

                var bodyBuilder = new BodyBuilder
                {
                    HtmlBody = htmlBody
                };
                message.Body = bodyBuilder.ToMessageBody();

                using var client = new SmtpClient();
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

                // Log successful email
                await LogEmailAsync(toEmail, subject, htmlBody, emailType, true, null);
                _logger.LogInformation("Email sent: {EmailType} to {ToEmail}", emailType, toEmail);
            }
            catch (Exception ex)
            {
                // Log failed email
                await LogEmailAsync(toEmail, subject, htmlBody, emailType, false, ex.Message);
                _logger.LogError(ex, "Failed to send email: {EmailType} to {ToEmail}", emailType, toEmail);
                throw;
            }
        }

        private async Task LogEmailAsync(string toEmail, string subject, string body, string emailType, bool success, string? errorMessage)
        {
            var emailLog = new EmailLog
            {
                ToEmail = toEmail,
                Subject = subject,
                Body = body,
                EmailType = emailType,
                IsSuccess = success,
                ErrorMessage = errorMessage,
                SentAt = DateTime.UtcNow
            };

            _context.EmailLogs.Add(emailLog);
            await _context.SaveChangesAsync();
        }
    }
}
