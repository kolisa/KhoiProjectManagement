namespace KhoiProjectManagement.Application
{
    public interface IEmailService
    {
        Task SendTaskAssignmentEmailAsync(string toEmail, string taskTitle, string projectName);
        Task SendOverdueTaskEmailAsync(string toEmail, string taskTitle, DateTime dueDate);
        Task SendProjectCreatedEmailAsync(string toEmail, string projectName);
        Task SendMentionEmailAsync(string toEmail, string mentionedByName, string contextLabel, string contextTitle, string commentBody);
        Task SendReminderDueEmailAsync(string toEmail, string reminderTitle, DateTime dueAt);
        Task SendPasswordResetEmailAsync(string toEmail, string userName, string resetLink);
        Task SendScheduledReportEmailAsync(string toEmail, string reportTitle, byte[] attachmentContent, string attachmentFileName, string attachmentContentType);
        Task SendTemporaryPasswordEmailAsync(string toEmail, string userName, string tempPassword);
    }
}
