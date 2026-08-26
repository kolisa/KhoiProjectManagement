namespace KhoiProjectManagement.Application
{
    public interface IGlobalSearchService
    {
        Task<GlobalSearchResultDto> SearchAsync(string query);
    }
}
