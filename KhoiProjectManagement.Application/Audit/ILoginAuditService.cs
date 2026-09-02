namespace KhoiProjectManagement.Application
{
    public interface ILoginAuditService
    {
        Task LogAsync(int? userId, string emailAttempted, bool success, string? failureReason, string? ipAddress);

        Task<List<LoginAuditLogDto>> GetRecentAsync(int take = 200, bool? success = null, string? emailContains = null);
    }
}
