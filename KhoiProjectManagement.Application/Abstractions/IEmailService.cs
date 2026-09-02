namespace KhoiProjectManagement.Application
{
    public interface IEmailService
    {
        Task SendTaskAssignmentEmailAsync(string toEmail, string taskTitle, string projectName);
        Task SendOverdueTaskEmailAsync(string toEmail, string taskTitle, DateTime dueDate);
        Task SendProjectCreatedEmailAsync(string toEmail, string projectName);
        Task SendMentionEmailAsync(string toEmail, string mentionedByName, string contextLabel, string contextTitle, string commentBody, string? contextUrl = null);
        Task SendReminderDueEmailAsync(string toEmail, string reminderTitle, DateTime dueAt);
        Task SendPasswordResetEmailAsync(string toEmail, string userName, string resetLink);
        Task SendScheduledReportEmailAsync(string toEmail, string reportTitle, byte[] attachmentContent, string attachmentFileName, string attachmentContentType);
        Task SendTemporaryPasswordEmailAsync(string toEmail, string userName, string tempPassword);
        Task SendLoginReminderEmailAsync(string toEmail, string userName, int daysSinceInvite);
        Task SendWeeklyDigestEmailAsync(string toEmail, string userName, int tasksCompleted, int tasksOpen, int tasksOverdue, int projectsActive, int libraryUploads, DateTime weekStart, DateTime weekEnd);
        Task SendNoDocumentsNudgeEmailAsync(string toEmail, string userName);
        Task SendDormantUserNudgeEmailAsync(string toEmail, string userName, int daysSinceLastLogin);
        Task SendBirthdayEmailAsync(string toEmail, string userName);
        Task SendTimesheetSubmittedEmailAsync(string toEmail, string submitterName, DateTime periodStart, DateTime periodEnd, decimal totalHours);

        // Called by SendQueuedEmailsJob (Quartz) - dispatches every EmailLog row still Status=Pending
        // (i.e. every Send*EmailAsync call above except SendScheduledReportEmailAsync, which still
        // sends synchronously - see EmailService for why). Never called from a request path.
        Task DispatchPendingEmailsAsync();
    }
}
