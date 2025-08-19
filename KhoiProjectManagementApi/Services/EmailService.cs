using KhoiProjectManagement.Models;
using KhoiProjectManagementApi.Data;
using MailKit.Net.Smtp;
using MimeKit;


namespace KhoiProjectManagementApi.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ProjectManagementContext _context;

        public EmailService(IConfiguration configuration, ProjectManagementContext context)
        {
            _configuration = configuration;
            _context = context;
        }

        public async Task SendTaskAssignmentEmailAsync(string toEmail, string taskTitle, string projectName)
        {
            var subject = $"Task Assignment: {taskTitle}";
            var body = $@"
                <h2>New Task Assignment</h2>
                <p>You have been assigned to a new task:</p>
                <p><strong>Task:</strong> {taskTitle}</p>
                <p><strong>Project:</strong> {projectName}</p>
                <p>Please log in to the Project Management System to view the details.</p>
                <br>
                <p>Best regards,<br>Project Management Team</p>
            ";

            await SendEmailAsync(toEmail, subject, body, "task_assignment");
        }

        public async Task SendOverdueTaskEmailAsync(string toEmail, string taskTitle, DateTime dueDate)
        {
            var subject = $"Overdue Task: {taskTitle}";
            var body = $@"
                <h2>Task Overdue Notification</h2>
                <p>The following task is overdue and requires your attention:</p>
                <p><strong>Task:</strong> {taskTitle}</p>
                <p><strong>Due Date:</strong> {dueDate:yyyy-MM-dd}</p>
                <p><strong>Days Overdue:</strong> {(DateTime.Now - dueDate).Days}</p>
                <p>Please update the task status as soon as possible.</p>
                <br>
                <p>Best regards,<br>Project Management Team</p>
            ";

            await SendEmailAsync(toEmail, subject, body, "overdue_reminder");
        }

        public async Task SendProjectCreatedEmailAsync(string toEmail, string projectName)
        {
            var subject = $"Project Created: {projectName}";
            var body = $@"
                <h2>New Project Created</h2>
                <p>A new project has been created and you have been added as a team member:</p>
                <p><strong>Project:</strong> {projectName}</p>
                <p>Please log in to the Project Management System to view the project details.</p>
                <br>
                <p>Best regards,<br>Project Management Team</p>
            ";

            await SendEmailAsync(toEmail, subject, body, "project_created");
        }

        private async Task SendEmailAsync(string toEmail, string subject, string htmlBody, string emailType)
        {
            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(
                    _configuration["Email:FromName"],
                    _configuration["Email:FromAddress"]
                ));
                message.To.Add(new MailboxAddress("", toEmail));
                message.Subject = subject;

                var bodyBuilder = new BodyBuilder
                {
                    HtmlBody = htmlBody
                };
                message.Body = bodyBuilder.ToMessageBody();

                using var client = new SmtpClient();
                await client.ConnectAsync(
                    _configuration["Email:SmtpHost"],
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
            }
            catch (Exception ex)
            {
                // Log failed email
                await LogEmailAsync(toEmail, subject, htmlBody, emailType, false, ex.Message);
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
