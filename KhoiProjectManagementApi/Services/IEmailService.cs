namespace KhoiProjectManagementApi.Services
{
    public interface IEmailService
    {
        Task SendTaskAssignmentEmailAsync(string toEmail, string taskTitle, string projectName);
        Task SendOverdueTaskEmailAsync(string toEmail, string taskTitle, DateTime dueDate);
        Task SendProjectCreatedEmailAsync(string toEmail, string projectName);
        Task SendMentionEmailAsync(string toEmail, string mentionedByName, string contextLabel, string contextTitle, string commentBody);
    }
}
