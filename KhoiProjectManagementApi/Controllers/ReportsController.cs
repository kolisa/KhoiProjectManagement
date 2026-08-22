using KhoiProjectManagement.Models.DTOs;
using KhoiProjectManagementApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KhoiProjectManagementApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "reports.view")]
    public class ReportsController : ControllerBase
    {
        private readonly IReportService _reportService;

        public ReportsController(IReportService reportService)
        {
            _reportService = reportService;
        }

        [HttpGet("project-summary")]
        public async Task<ActionResult<ProjectSummaryReportDto>> GetProjectSummaryReport()
        {
            var report = await _reportService.GenerateProjectSummaryReportAsync();
            return Ok(report);
        }

        [HttpGet("team-performance")]
        public async Task<ActionResult<TeamPerformanceReportDto>> GetTeamPerformanceReport()
        {
            var report = await _reportService.GenerateTeamPerformanceReportAsync();
            return Ok(report);
        }

        [HttpGet("overdue-tasks")]
        public async Task<ActionResult<OverdueTasksReportDto>> GetOverdueTasksReport()
        {
            var report = await _reportService.GenerateOverdueTasksReportAsync();
            return Ok(report);
        }
    }
}
