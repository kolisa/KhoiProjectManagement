using System.Security.Claims;
using KhoiProjectManagement.Application;
using KhoiProjectManagement.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KhoiProjectManagementApi.Controllers
{
    // Company-wide calendar - birthdays, events, promotions - visible to every authenticated user
    // with no permission gate on reads; calendar.manage only gates writing CompanyEvent rows (see
    // plan Phase 12). Birthdays are never manually created - there's nothing to manage there.
    [ApiController]
    [Authorize]
    public class CalendarController : ControllerBase
    {
        private readonly ICalendarService _calendarService;

        public CalendarController(ICalendarService calendarService)
        {
            _calendarService = calendarService;
        }

        [HttpGet("api/calendar")]
        public async Task<IActionResult> GetFeed([FromQuery] DateTime from, [FromQuery] DateTime to)
        {
            return Ok(await _calendarService.GetFeedAsync(from, to));
        }

        [HttpPost("api/calendar/events")]
        [Authorize(Policy = "calendar.manage")]
        public async Task<IActionResult> CreateEvent(CreateCompanyEventDto dto)
        {
            var callerId = GetUserId();
            var created = await _calendarService.CreateEventAsync(dto, callerId);
            return CreatedAtAction(nameof(GetFeed), created);
        }

        [HttpPut("api/calendar/events/{id}")]
        [Authorize(Policy = "calendar.manage")]
        public async Task<IActionResult> UpdateEvent(int id, CreateCompanyEventDto dto)
        {
            var updated = await _calendarService.UpdateEventAsync(id, dto);
            if (!updated)
                return NotFound();

            return NoContent();
        }

        [HttpDelete("api/calendar/events/{id}")]
        [Authorize(Policy = "calendar.manage")]
        public async Task<IActionResult> DeleteEvent(int id)
        {
            var deleted = await _calendarService.DeleteEventAsync(id);
            if (!deleted)
                return NotFound();

            return NoContent();
        }

        [HttpPut("api/users/{id}/date-of-birth")]
        public async Task<IActionResult> SetDateOfBirth(int id, SetDateOfBirthDto dto)
        {
            if (id != GetUserId() && !User.HasClaim("permission", "users.edit"))
                return Forbid();

            var updated = await _calendarService.SetDateOfBirthAsync(id, dto.DateOfBirth);
            if (!updated)
                return NotFound();

            return NoContent();
        }

        // Issues a fresh subscription token for the caller (invalidating any previous one) - the raw
        // token is returned here only, never stored or retrievable again (see
        // CalendarService.RegenerateFeedTokenAsync). The frontend builds the full .ics URL around it.
        [HttpPost("api/calendar/feed-token/regenerate")]
        public async Task<IActionResult> RegenerateFeedToken()
        {
            var token = await _calendarService.RegenerateFeedTokenAsync(GetUserId());
            return Ok(new { token });
        }

        [HttpGet("api/calendar/feed-token/status")]
        public async Task<IActionResult> GetFeedTokenStatus()
        {
            var hasToken = await _calendarService.HasFeedTokenAsync(GetUserId());
            return Ok(new { hasToken });
        }

        // No [Authorize] here - a calendar app subscribing to this URL can't send an Authorization
        // header, so the opaque token in the query string IS the credential (hashed at rest, same as
        // RefreshToken/PasswordResetToken - see User.CalendarFeedTokenHash).
        [AllowAnonymous]
        [HttpGet("api/calendar/feed.ics")]
        public async Task<IActionResult> GetIcsFeed([FromQuery] string token)
        {
            var ics = await _calendarService.GetIcsFeedAsync(token);
            if (ics == null)
                return NotFound();

            return Content(ics, "text/calendar");
        }

        private int GetUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)!;
            return int.Parse(claim.Value);
        }
    }
}
