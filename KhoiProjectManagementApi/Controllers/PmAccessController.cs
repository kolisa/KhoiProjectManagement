using System.Security.Claims;
using KhoiProjectManagement.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KhoiProjectManagementApi.Controllers
{
    // Grants/revokes Project Management module access. Gated on pm.admin (SuperAdmin always passes) and
    // deliberately NOT [AllowModuleOnly]: a module-only account must never administer access.
    [ApiController]
    [Route("api/pm/access")]
    [Authorize(Policy = "pm.admin")]
    public class PmAccessController : ControllerBase
    {
        private readonly IPmProjectService _service;

        public PmAccessController(IPmProjectService service)
        {
            _service = service;
        }

        [HttpGet("users")]
        public async Task<ActionResult<List<PmAccessUserDto>>> GetUsers() =>
            Ok(await _service.GetAccessUsersAsync());

        [HttpPut("users/{userId:int}")]
        public async Task<IActionResult> SetAccess(int userId, SetPmAccessDto dto)
        {
            var actingUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            await _service.SetAccessAsync(actingUserId, userId, dto);
            return NoContent();
        }
    }
}
