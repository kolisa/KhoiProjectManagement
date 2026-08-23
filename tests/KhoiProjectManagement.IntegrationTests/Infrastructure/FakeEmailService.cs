using System.Collections.Concurrent;
using KhoiProjectManagement.Application;

namespace KhoiProjectManagement.IntegrationTests.Infrastructure
{
    // Swapped in for the real MailKit-backed EmailService in every functional/integration test run - the
    // real one would attempt actual SMTP sends against smtp.gmail.com using the credentials committed in
    // appsettings.json, which Program.cs's "trigger jobs immediately on boot" call (OverdueTaskCheckJob/
    // ReminderDueCheckJob) can reach on every ApiWebApplicationFactory startup. Records every call so
    // tests can assert an email was (or wasn't) sent without touching a real mailbox.
    public class FakeEmailService : IEmailService
    {
        public record SentEmail(string Method, string ToEmail);

        private readonly ConcurrentQueue<SentEmail> _sent = new();
        public IReadOnlyCollection<SentEmail> SentEmails => _sent.ToArray();

        public Task SendTaskAssignmentEmailAsync(string toEmail, string taskTitle, string projectName) =>
            Record(nameof(SendTaskAssignmentEmailAsync), toEmail);

        public Task SendOverdueTaskEmailAsync(string toEmail, string taskTitle, DateTime dueDate) =>
            Record(nameof(SendOverdueTaskEmailAsync), toEmail);

        public Task SendProjectCreatedEmailAsync(string toEmail, string projectName) =>
            Record(nameof(SendProjectCreatedEmailAsync), toEmail);

        public Task SendMentionEmailAsync(string toEmail, string mentionedByName, string contextLabel, string contextTitle, string commentBody) =>
            Record(nameof(SendMentionEmailAsync), toEmail);

        public Task SendReminderDueEmailAsync(string toEmail, string reminderTitle, DateTime dueAt) =>
            Record(nameof(SendReminderDueEmailAsync), toEmail);

        public Task SendPasswordResetEmailAsync(string toEmail, string userName, string resetLink) =>
            Record(nameof(SendPasswordResetEmailAsync), toEmail);

        private Task Record(string method, string toEmail)
        {
            _sent.Enqueue(new SentEmail(method, toEmail));
            return Task.CompletedTask;
        }
    }
}
