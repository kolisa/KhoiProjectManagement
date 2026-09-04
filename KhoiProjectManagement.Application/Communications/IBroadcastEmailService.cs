namespace KhoiProjectManagement.Application
{
    public interface IBroadcastEmailService
    {
        // Returns the number of active users the email was actually queued for (the roles selected
        // might resolve to zero users, or overlap - recipients are deduplicated).
        Task<int> SendBroadcastAsync(BroadcastEmailDto dto);
    }
}
