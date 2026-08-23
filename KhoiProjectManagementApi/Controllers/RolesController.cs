using System.Security.Claims;
using KhoiProjectManagement.Application;
using KhoiProjectManagement.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KhoiProjectManagementApi.Controllers
{
    // Runtime configuration of role->permission mappings, replacing the fixed HasData seed as the
    // only way to change who holds what (see plan Phase 9). Gated by the same users.manage_roles
    // permission that already gates assigning roles to users (2.3) - "who can hold what" and "what a
    // role can do" are the same admin capability.
    [ApiController]
    [Authorize(Policy = "users.manage_roles")]
    public class RolesController : ControllerBase
    {
        private readonly IRoleService _roleService;

        public RolesController(IRoleService roleService)
        {
            _roleService = roleService;
        }

        [HttpGet("api/roles")]
        public async Task<IActionResult> GetRoles()
        {
            return Ok(await _roleService.GetRolesAsync());
        }

        [HttpGet("api/permissions")]
        public async Task<IActionResult> GetPermissions()
        {
            return Ok(await _roleService.GetAllPermissionsAsync());
        }

        [HttpGet("api/roles/{id}/permissions")]
        public async Task<IActionResult> GetRolePermissions(int id)
        {
            var permissions = await _roleService.GetRolePermissionsAsync(id);
            if (permissions == null)
                return NotFound();

            return Ok(permissions);
        }

        [HttpPut("api/roles/{id}/permissions")]
        public async Task<IActionResult> SetRolePermissions(int id, SetRolePermissionsDto dto)
        {
            try
            {
                var updated = await _roleService.SetRolePermissionsAsync(id, dto.PermissionNames, GetUserId());
                if (!updated)
                    return NotFound();

                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("api/roles")]
        public async Task<IActionResult> CreateRole(CreateRoleDto dto)
        {
            var role = await _roleService.CreateRoleAsync(dto);
            return CreatedAtAction(nameof(GetRoles), role);
        }

        [HttpPut("api/roles/{id}")]
        public async Task<IActionResult> UpdateRole(int id, UpdateRoleDto dto)
        {
            try
            {
                var updated = await _roleService.UpdateRoleAsync(id, dto);
                if (!updated)
                    return NotFound();

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
