namespace KhoiProjectManagement.Application
{
    public interface IPageVisitService
    {
        Task LogAsync(int userId, string tabKey);

        Task<List<PageVisitLogDto>> GetRecentAsync(int take = 200, int? userId = null, string? tabKey = null);
    }
}
