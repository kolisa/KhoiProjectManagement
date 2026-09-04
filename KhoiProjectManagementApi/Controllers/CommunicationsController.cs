using System.Security.Claims;
using KhoiProjectManagement.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KhoiProjectManagementApi.Controllers
{
    // Admin communications: broadcast email (email.broadcast) and the weekly system-overview email's
    // on/off switch + schedule (email.manage_overview). Bare class-level [Authorize] (matches
    // AuditController's pattern for a controller with multiple independently-gated actions) - each
    // action opts into its own specific policy instead, since a class-level policy here would AND
    // together with a method-level one and wrongly require both permissions at once.
    [ApiController]
    [Route("api/communications")]
    [Authorize]
    public class CommunicationsController : ControllerBase
    {
        private readonly IBroadcastEmailService _broadcastEmailService;
        private readonly ISystemOverviewEmailSettingsService _systemOverviewEmailSettingsService;

        public CommunicationsController(
            IBroadcastEmailService broadcastEmailService,
            ISystemOverviewEmailSettingsService systemOverviewEmailSettingsService)
        {
            _broadcastEmailService = broadcastEmailService;
            _systemOverviewEmailSettingsService = systemOverviewEmailSettingsService;
        }

        [HttpPost("broadcast")]
        [Authorize(Policy = "email.broadcast")]
        public async Task<IActionResult> SendBroadcast(BroadcastEmailDto dto)
        {
            var recipientCount = await _broadcastEmailService.SendBroadcastAsync(dto);
            return Ok(new BroadcastEmailResultDto { RecipientCount = recipientCount });
        }

        [HttpGet("system-overview-email-settings")]
        [Authorize(Policy = "email.manage_overview")]
        public async Task<IActionResult> GetSystemOverviewEmailSettings()
        {
            return Ok(await _systemOverviewEmailSettingsService.GetAsync());
        }

        [HttpPut("system-overview-email-settings")]
        [Authorize(Policy = "email.manage_overview")]
        public async Task<IActionResult> UpdateSystemOverviewEmailSettings(UpdateSystemOverviewEmailSettingsDto dto)
        {
            return Ok(await _systemOverviewEmailSettingsService.UpdateAsync(dto, GetUserId()));
        }

        private int GetUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)!;
            return int.Parse(claim.Value);
        }
    }
}
