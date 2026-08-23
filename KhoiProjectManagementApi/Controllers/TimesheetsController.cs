using KhoiProjectManagement.Application;
using KhoiProjectManagement.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KhoiProjectManagementApi.Controllers
{
    // Flat, ownership-based - any user can always manage their own timesheet with no permission
    // needed (see plan Phase 10). timesheets.approve/timesheets.view_all only matter for someone
    // else's timesheet, checked inside TimesheetService.
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TimesheetsController : ControllerBase
    {
        private readonly ITimesheetService _timesheetService;

        public TimesheetsController(ITimesheetService timesheetService)
        {
            _timesheetService = timesheetService;
        }

        [HttpGet]
        public async Task<IActionResult> GetTimesheets([FromQuery] int? userId, [FromQuery] string? status)
        {
            try
            {
                return Ok(await _timesheetService.GetTimesheetsAsync(userId, status, User));
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetTimesheet(int id)
        {
            try
            {
                var timesheet = await _timesheetService.GetTimesheetByIdAsync(id, User);
                if (timesheet == null)
                    return NotFound();

                return Ok(timesheet);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateTimesheet(CreateTimesheetDto dto)
        {
            var timesheet = await _timesheetService.CreateTimesheetAsync(dto, User);
            return CreatedAtAction(nameof(GetTimesheet), new { id = timesheet.Id }, timesheet);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateTimesheet(int id, UpdateTimesheetDto dto)
        {
            try
            {
                var updated = await _timesheetService.UpdateTimesheetAsync(id, dto, User);
                if (!updated)
                    return NotFound();

                return NoContent();
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("{id}/submit")]
        public async Task<IActionResult> SubmitTimesheet(int id)
        {
            try
            {
                var submitted = await _timesheetService.SubmitTimesheetAsync(id, User);
                if (!submitted)
                    return NotFound();

                return NoContent();
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("{id}/approve")]
        [Authorize(Policy = "timesheets.approve")]
        public async Task<IActionResult> ApproveTimesheet(int id)
        {
            try
            {
                var approved = await _timesheetService.ApproveTimesheetAsync(id, User);
                if (!approved)
                    return NotFound();

                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("{id}/reject")]
        [Authorize(Policy = "timesheets.approve")]
        public async Task<IActionResult> RejectTimesheet(int id, RejectTimesheetDto dto)
        {
            try
            {
                var rejected = await _timesheetService.RejectTimesheetAsync(id, dto.Reason, User);
                if (!rejected)
                    return NotFound();

                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
