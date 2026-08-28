using KhoiProjectManagement.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KhoiProjectManagementApi.Controllers
{
    // Admin-only visibility into what the system has done behind the scenes: emails it sent (EmailLog,
    // previously write-only - see EmailService.LogEmailAsync) and application error logs (Serilog's
    // rolling-daily files under Logs/ - see ILogFileService). One controller, one permission, matching
    // how the frontend presents both as a single "Audit" Settings section.
    [ApiController]
    [Route("api/audit")]
    [Authorize(Policy = "audit.view")]
    public class AuditController : ControllerBase
    {
        private readonly IEmailLogService _emailLogService;
        private readonly ILogFileService _logFileService;

        public AuditController(IEmailLogService emailLogService, ILogFileService logFileService)
        {
            _emailLogService = emailLogService;
            _logFileService = logFileService;
        }

        [HttpGet("emails")]
        public async Task<IActionResult> GetEmailLog(
            [FromQuery] int take = 200,
            [FromQuery] string? status = null,
            [FromQuery] string? emailType = null,
            [FromQuery] string? toEmailContains = null)
        {
            var result = await _emailLogService.GetRecentAsync(take, status, emailType, toEmailContains);
            return Ok(result);
        }

        [HttpGet("error-logs/dates")]
        public async Task<IActionResult> GetErrorLogDates()
        {
            var dates = await _logFileService.GetAvailableDatesAsync();
            return Ok(dates.Select(d => d.ToString("yyyy-MM-dd")));
        }

        [HttpGet("error-logs")]
        public async Task<IActionResult> GetErrorLogs(
            [FromQuery] string date,
            [FromQuery] string? level = null,
            [FromQuery] int take = 200)
        {
            if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", out var parsedDate))
                return BadRequest(new { message = "date must be in yyyy-MM-dd format." });

            var entries = await _logFileService.GetRecentEntriesAsync(parsedDate, level, take);
            return Ok(entries);
        }
    }
}
