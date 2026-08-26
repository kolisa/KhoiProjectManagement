using KhoiProjectManagement.Application;
using KhoiProjectManagement.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KhoiProjectManagementApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;

        public UsersController(IUserService userService)
        {
            _userService = userService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<TeamMemberDto>>> GetUsers([FromQuery] bool includeInactive = false)
        {
            var users = await _userService.GetAllUsersAsync(includeInactive);
            return Ok(users);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<TeamMemberDto>> GetUser(int id)
        {
            var user = await _userService.GetUserByIdAsync(id);
            if (user == null)
                return NotFound();

            return Ok(user);
        }

        [HttpPost]
        [Authorize(Policy = "users.create")]
        public async Task<ActionResult<TeamMemberDto>> CreateUser(CreateAdminUserDto createUserDto)
        {
            try
            {
                var user = await _userService.CreateUserWithTempPasswordAsync(createUserDto);
                return CreatedAtAction(nameof(GetUser), new { id = user.Id }, user);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("{id}")]
        [Authorize(Policy = "users.edit")]
        public async Task<IActionResult> UpdateUser(int id, UpdateUserProfileDto updateUserDto)
        {
            var updated = await _userService.UpdateUserAsync(id, updateUserDto);
            if (!updated)
                return NotFound();

            return NoContent();
        }

        [HttpPut("{id}/roles")]
        [Authorize(Policy = "users.manage_roles")]
        public async Task<IActionResult> AssignRoles(int id, AssignUserRolesDto assignUserRolesDto)
        {
            var assigned = await _userService.AssignRolesAsync(id, assignUserRolesDto.RoleIds);
            if (!assigned)
                return NotFound();

            return NoContent();
        }

        [HttpDelete("{id}")]
        [Authorize(Policy = "users.delete")]
        public async Task<IActionResult> DeactivateUser(int id)
        {
            var deactivated = await _userService.DeactivateUserAsync(id);
            if (!deactivated)
                return NotFound();

            return NoContent();
        }

        [HttpPost("{id}/reactivate")]
        [Authorize(Policy = "users.delete")]
        public async Task<IActionResult> ReactivateUser(int id)
        {
            var reactivated = await _userService.ReactivateUserAsync(id);
            if (!reactivated)
                return NotFound();

            return NoContent();
        }

        [HttpPost("{id}/resend-temp-password")]
        [Authorize(Policy = "users.edit")]
        public async Task<IActionResult> ResendTempPassword(int id)
        {
            try
            {
                await _userService.ResendTempPasswordAsync(id);
                return NoContent();
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
