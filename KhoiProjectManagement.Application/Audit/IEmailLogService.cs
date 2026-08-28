namespace KhoiProjectManagement.Application
{
    public interface IEmailLogService
    {
        // status is one of "Pending"/"Sent"/"Failed" (matches EmailLogStatus's names), or null for all.
        Task<List<EmailLogDto>> GetRecentAsync(int take = 200, string? status = null, string? emailType = null, string? toEmailContains = null);
    }
}
