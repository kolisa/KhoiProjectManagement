using KhoiProjectManagement.Domain;
using KhoiProjectManagement.Application;
using KhoiProjectManagement.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace KhoiProjectManagementApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class NotificationsController : ControllerBase
    {
        private readonly INotificationService _notificationService;

        public NotificationsController(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Notification>>> GetNotifications()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
                return Unauthorized();

            var userId = int.Parse(userIdClaim.Value);
            var notifications = await _notificationService.GetUserNotificationsAsync(userId);
            return Ok(notifications);
        }

        [HttpPut("{id}/read")]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            await _notificationService.MarkAsReadAsync(id);
            return NoContent();
        }

        [HttpPost("check-overdue")]
        [Authorize(Policy = "notifications.check_overdue")]
        public async Task<IActionResult> CheckOverdueTasks()
        {
            await _notificationService.CheckOverdueTasksAsync();
            return Ok(new { message = "Overdue tasks checked and notifications sent" });
        }

        // Personal preferences - always self, no permission needed beyond being logged in.
        [HttpGet("preferences")]
        public async Task<ActionResult<List<NotificationPreferenceDto>>> GetPreferences()
        {
            var userId = GetUserId();
            return Ok(await _notificationService.GetPreferencesAsync(userId));
        }

        [HttpPut("preferences")]
        public async Task<IActionResult> SetPreferences(List<UpdateNotificationPreferenceDto> updates)
        {
            try
            {
                await _notificationService.SetPreferencesAsync(GetUserId(), updates);
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        private int GetUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)!;
            return int.Parse(claim.Value);
        }
    }
}
