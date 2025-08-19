using KhoiProjectManagement.Models.DTOs;

namespace KhoiProjectManagementApi.Services
{
    public interface IReportService
    {
        Task<ProjectSummaryReportDto> GenerateProjectSummaryReportAsync();
        Task<TeamPerformanceReportDto> GenerateTeamPerformanceReportAsync();
        Task<OverdueTasksReportDto> GenerateOverdueTasksReportAsync();
    }
}
