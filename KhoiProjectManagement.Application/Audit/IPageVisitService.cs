namespace KhoiProjectManagement.Application
{
    public interface IPageVisitService
    {
        // Returns the new row's id - the caller (App.jsx) holds onto it to later attach a duration to
        // this exact visit via RecordDurationAsync once the user navigates away.
        Task<int> LogAsync(int userId, string tabKey);

        Task<List<PageVisitLogDto>> GetRecentAsync(int take = 200, int? userId = null, string? tabKey = null);

        // Best-effort: silently no-ops if the visit doesn't exist or belongs to a different user
        // (never throws for that - this is called from a fire-and-forget client-side timer, not a
        // request a user is watching for confirmation).
        Task RecordDurationAsync(int id, int userId, int durationSeconds);
    }
}
