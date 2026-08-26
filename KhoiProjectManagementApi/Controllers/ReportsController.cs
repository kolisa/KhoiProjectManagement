using System.Security.Claims;
using KhoiProjectManagement.Application;
using KhoiProjectManagement.Infrastructure.Services;
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
        private readonly IReportExportService _reportExportService;
        private readonly IReportScheduleService _reportScheduleService;

        public ReportsController(IReportService reportService, IReportExportService reportExportService, IReportScheduleService reportScheduleService)
        {
            _reportService = reportService;
            _reportExportService = reportExportService;
            _reportScheduleService = reportScheduleService;
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

        [HttpPost("{reportType}/export")]
        public async Task<IActionResult> ExportReport(string reportType, [FromQuery] string format)
        {
            try
            {
                var (content, contentType, fileName) = await _reportExportService.ExportReportAsync(reportType, format, GetUserId());
                return File(content, contentType, fileName);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("exports/recent")]
        public async Task<ActionResult<List<ReportExportHistoryDto>>> GetRecentExports()
        {
            return Ok(await _reportScheduleService.GetRecentExportsAsync());
        }

        [HttpGet("exports/{id}/download")]
        public async Task<IActionResult> DownloadExport(int id)
        {
            var export = await _reportExportService.DownloadExportAsync(id);
            if (export == null)
                return NotFound();

            return File(export.Value.Content, export.Value.ContentType, export.Value.FileName);
        }

        [HttpGet("schedules")]
        public async Task<ActionResult<List<ScheduledReportDto>>> GetSchedules()
        {
            return Ok(await _reportScheduleService.GetSchedulesAsync());
        }

        [HttpPost("schedules")]
        public async Task<ActionResult<ScheduledReportDto>> CreateSchedule(CreateScheduledReportDto dto)
        {
            try
            {
                var schedule = await _reportScheduleService.CreateScheduleAsync(dto, GetUserId());
                return Ok(schedule);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("schedules/{id}")]
        public async Task<IActionResult> DeleteSchedule(int id)
        {
            var deleted = await _reportScheduleService.DeleteScheduleAsync(id);
            if (!deleted)
                return NotFound();

            return NoContent();
        }

        private int GetUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)!;
            return int.Parse(claim.Value);
        }
    }
}
