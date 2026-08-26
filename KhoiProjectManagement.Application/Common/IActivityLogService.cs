namespace KhoiProjectManagement.Application
{
    public interface IActivityLogService
    {
        Task LogAsync(string entityType, int? entityId, string entityNameSnapshot, int actorUserId, string action, string? details = null);

        Task<List<ActivityLogEntryDto>> GetRecentAsync(int take = 20);
    }
}
