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

        public Task SendMentionEmailAsync(string toEmail, string mentionedByName, string contextLabel, string contextTitle, string commentBody, string? contextUrl = null) =>
            Record(nameof(SendMentionEmailAsync), toEmail);

        public Task SendReminderDueEmailAsync(string toEmail, string reminderTitle, DateTime dueAt) =>
            Record(nameof(SendReminderDueEmailAsync), toEmail);

        public Task SendPasswordResetEmailAsync(string toEmail, string userName, string resetLink) =>
            Record(nameof(SendPasswordResetEmailAsync), toEmail);

        public Task SendScheduledReportEmailAsync(string toEmail, string reportTitle, byte[] attachmentContent, string attachmentFileName, string attachmentContentType) =>
            Record(nameof(SendScheduledReportEmailAsync), toEmail);

        public Task SendTemporaryPasswordEmailAsync(string toEmail, string userName, string tempPassword) =>
            Record(nameof(SendTemporaryPasswordEmailAsync), toEmail);

        public Task SendLoginReminderEmailAsync(string toEmail, string userName, int daysSinceInvite) =>
            Record(nameof(SendLoginReminderEmailAsync), toEmail);

        public Task SendWeeklyDigestEmailAsync(string toEmail, string userName, int tasksCompleted, int tasksOpen, int tasksOverdue, int projectsActive, int libraryUploads, DateTime weekStart, DateTime weekEnd) =>
            Record(nameof(SendWeeklyDigestEmailAsync), toEmail);

        public Task SendNoDocumentsNudgeEmailAsync(string toEmail, string userName) =>
            Record(nameof(SendNoDocumentsNudgeEmailAsync), toEmail);

        public Task SendDormantUserNudgeEmailAsync(string toEmail, string userName, int daysSinceLastLogin) =>
            Record(nameof(SendDormantUserNudgeEmailAsync), toEmail);

        public Task SendBirthdayEmailAsync(string toEmail, string userName) =>
            Record(nameof(SendBirthdayEmailAsync), toEmail);

        public Task SendTimesheetSubmittedEmailAsync(string toEmail, string submitterName, DateTime periodStart, DateTime periodEnd, decimal totalHours) =>
            Record(nameof(SendTimesheetSubmittedEmailAsync), toEmail);

        public Task SendBroadcastEmailAsync(string toEmail, string subject, string bodyHtml) =>
            Record(nameof(SendBroadcastEmailAsync), toEmail);

        public Task SendSystemOverviewEmailAsync(string toEmail, string userName) =>
            Record(nameof(SendSystemOverviewEmailAsync), toEmail);

        public Task DispatchPendingEmailsAsync() => Task.CompletedTask;

        private Task Record(string method, string toEmail)
        {
            _sent.Enqueue(new SentEmail(method, toEmail));
            return Task.CompletedTask;
        }
    }
}
