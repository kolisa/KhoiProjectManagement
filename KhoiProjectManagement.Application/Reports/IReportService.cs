using KhoiProjectManagement.Application;

namespace KhoiProjectManagement.Application
{
    public interface IReportService
    {
        Task<ProjectSummaryReportDto> GenerateProjectSummaryReportAsync();
        Task<TeamPerformanceReportDto> GenerateTeamPerformanceReportAsync();
        Task<OverdueTasksReportDto> GenerateOverdueTasksReportAsync();
    }
}
