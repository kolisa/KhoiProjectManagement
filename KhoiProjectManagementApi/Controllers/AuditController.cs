using System.Security.Claims;
using KhoiProjectManagement.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KhoiProjectManagementApi.Controllers
{
    // Admin-only visibility into what the system has done behind the scenes: emails it sent (EmailLog,
    // previously write-only - see EmailService.LogEmailAsync), application error logs (Serilog's
    // rolling-daily files under Logs/ - see ILogFileService), the login audit trail, and page (tab)
    // visits. One controller, matching how the frontend presents all of these as a single "Audit"
    // Settings section. Every read here is gated by audit.view, applied per-action rather than at the
    // class level because the page-visit POST below must be callable by any authenticated user logging
    // their own navigation, not just admins - it falls back to the bare class-level [Authorize].
    [ApiController]
    [Route("api/audit")]
    [Authorize]
    public class AuditController : ControllerBase
    {
        private readonly IEmailLogService _emailLogService;
        private readonly ILogFileService _logFileService;
        private readonly ILoginAuditService _loginAuditService;
        private readonly IPageVisitService _pageVisitService;

        public AuditController(
            IEmailLogService emailLogService,
            ILogFileService logFileService,
            ILoginAuditService loginAuditService,
            IPageVisitService pageVisitService)
        {
            _emailLogService = emailLogService;
            _logFileService = logFileService;
            _loginAuditService = loginAuditService;
            _pageVisitService = pageVisitService;
        }

        [HttpGet("emails")]
        [Authorize(Policy = "audit.view")]
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
        [Authorize(Policy = "audit.view")]
        public async Task<IActionResult> GetErrorLogDates()
        {
            var dates = await _logFileService.GetAvailableDatesAsync();
            return Ok(dates.Select(d => d.ToString("yyyy-MM-dd")));
        }

        [HttpGet("error-logs")]
        [Authorize(Policy = "audit.view")]
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

        [HttpGet("logins")]
        [Authorize(Policy = "audit.view")]
        public async Task<IActionResult> GetLoginAuditLog(
            [FromQuery] int take = 200,
            [FromQuery] bool? success = null,
            [FromQuery] string? emailContains = null)
        {
            var result = await _loginAuditService.GetRecentAsync(take, success, emailContains);
            return Ok(result);
        }

        [HttpGet("page-visits")]
        [Authorize(Policy = "audit.view")]
        public async Task<IActionResult> GetPageVisitLog(
            [FromQuery] int take = 200,
            [FromQuery] int? userId = null,
            [FromQuery] string? tabKey = null)
        {
            var result = await _pageVisitService.GetRecentAsync(take, userId, tabKey);
            return Ok(result);
        }

        // Any authenticated user logs their own navigation here - not admin-only, unlike every other
        // action in this controller (see class comment). Returns the new row's id so the frontend can
        // later attach a duration to this exact visit (see PATCH below) once the user navigates away.
        [HttpPost("page-visits")]
        public async Task<IActionResult> LogPageVisit([FromBody] LogPageVisitDto dto)
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)!;
            var userId = int.Parse(claim.Value);

            var id = await _pageVisitService.LogAsync(userId, dto.TabKey);
            return Ok(new { id });
        }

        // Same "any authenticated user, own data only" reasoning as the POST above - the service itself
        // silently no-ops rather than 404ing/403ing if the id doesn't belong to the caller, since this
        // is fired from a best-effort client-side timer with nothing watching the response.
        [HttpPatch("page-visits/{id}/duration")]
        public async Task<IActionResult> RecordPageVisitDuration(int id, [FromBody] RecordPageVisitDurationDto dto)
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)!;
            var userId = int.Parse(claim.Value);

            await _pageVisitService.RecordDurationAsync(id, userId, dto.DurationSeconds);
            return NoContent();
        }
    }

    public class LogPageVisitDto
    {
        public string TabKey { get; set; } = string.Empty;
    }
}
