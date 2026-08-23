using KhoiProjectManagement.Application;

namespace KhoiProjectManagement.Application
{
    public interface IDashboardService
    {
        Task<DashboardStatisticsDto> GetDashboardStatisticsAsync();
    }
}
