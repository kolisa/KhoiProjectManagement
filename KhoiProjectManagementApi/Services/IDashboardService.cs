using KhoiProjectManagement.Models.DTOs;

namespace KhoiProjectManagementApi.Services
{
    public interface IDashboardService
    {
        Task<DashboardStatisticsDto> GetDashboardStatisticsAsync();
    }
}
