using KhoiProjectManagement.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KhoiProjectManagementApi.Controllers
{
    // Admin broadcast email - a subject/body composed in the app and sent to every active user
    // holding at least one of the selected roles. Gated by email.broadcast, granted to Admin only
    // at seed time (see ProjectManagementContext.OnModelCreating).
    [ApiController]
    [Route("api/communications")]
    [Authorize(Policy = "email.broadcast")]
    public class CommunicationsController : ControllerBase
    {
        private readonly IBroadcastEmailService _broadcastEmailService;

        public CommunicationsController(IBroadcastEmailService broadcastEmailService)
        {
            _broadcastEmailService = broadcastEmailService;
        }

        [HttpPost("broadcast")]
        public async Task<IActionResult> SendBroadcast(BroadcastEmailDto dto)
        {
            var recipientCount = await _broadcastEmailService.SendBroadcastAsync(dto);
            return Ok(new BroadcastEmailResultDto { RecipientCount = recipientCount });
        }
    }
}
