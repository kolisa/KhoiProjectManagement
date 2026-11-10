using System.Security.Claims;
using KhoiProjectManagement.Application;
using KhoiProjectManagementApi.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KhoiProjectManagementApi.Controllers
{
    // Grants/revokes Project Management module access and sets each person's access level. Open to the module's
    // administrators - the Admin role (SuperAdmin) or a pm.admin holder - and deliberately NOT [AllowModuleOnly]: a module-only account must never administer access.
    [ApiController]
    [Route("api/pm/access")]
    [Authorize]
    public class PmAccessController : ControllerBase
    {
        private readonly IPmProjectService _service;

        public PmAccessController(IPmProjectService service)
        {
            _service = service;
        }

        private void RequireAdministrator()
        {
            var caller = User.ToPmCaller();
            if (!caller.IsSuperAdmin && !caller.IsPmAdmin)
                throw new ForbiddenException("Only an administrator can manage Project Management access.");
        }

        [HttpGet("users")]
        public async Task<ActionResult<List<PmAccessUserDto>>> GetUsers()
        {
            RequireAdministrator();
            return Ok(await _service.GetAccessUsersAsync());
        }

        [HttpPut("users/{userId:int}")]
        public async Task<IActionResult> SetAccess(int userId, SetPmAccessDto dto)
        {
            RequireAdministrator();
            var actingUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            await _service.SetAccessAsync(actingUserId, userId, dto);
            return NoContent();
        }
    }
}
