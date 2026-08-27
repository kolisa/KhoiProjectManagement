namespace KhoiProjectManagement.Application
{
    public interface IEmailLogService
    {
        Task<List<EmailLogDto>> GetRecentAsync(int take = 200, bool? isSuccess = null, string? emailType = null, string? toEmailContains = null);
    }
}
