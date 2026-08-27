using KhoiProjectManagement.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KhoiProjectManagementApi.Controllers
{
    // Ad-hoc, admin-managed collections of users, grantable as a SpacePermission target alongside
    // User and Role (see SpacesController's {id}/permissions routes) - gated by its own groups.manage
    // permission, mirroring how RolesController is gated by users.manage_roles.
    [ApiController]
    [Authorize(Policy = "groups.manage")]
    public class GroupsController : ControllerBase
    {
        private readonly IGroupService _groupService;

        public GroupsController(IGroupService groupService)
        {
            _groupService = groupService;
        }

        [HttpGet("api/groups")]
        public async Task<IActionResult> GetGroups()
        {
            return Ok(await _groupService.GetGroupsAsync());
        }

        [HttpPost("api/groups")]
        public async Task<IActionResult> CreateGroup(CreateGroupDto dto)
        {
            var group = await _groupService.CreateGroupAsync(dto);
            return CreatedAtAction(nameof(GetGroups), group);
        }

        [HttpPut("api/groups/{id}")]
        public async Task<IActionResult> UpdateGroup(int id, UpdateGroupDto dto)
        {
            var updated = await _groupService.UpdateGroupAsync(id, dto);
            if (!updated)
                return NotFound();

            return NoContent();
        }

        [HttpGet("api/groups/{id}/members")]
        public async Task<IActionResult> GetGroupMembers(int id)
        {
            var members = await _groupService.GetGroupMembersAsync(id);
            if (members == null)
                return NotFound();

            return Ok(members);
        }

        [HttpPut("api/groups/{id}/members")]
        public async Task<IActionResult> SetGroupMembers(int id, SetGroupMembersDto dto)
        {
            var updated = await _groupService.SetGroupMembersAsync(id, dto.UserIds);
            if (!updated)
                return NotFound();

            return NoContent();
        }
    }
}
