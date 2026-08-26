using System.Security.Claims;
using KhoiProjectManagement.Domain;
using KhoiProjectManagement.Application.Authorization;
using KhoiProjectManagement.Application;
using KhoiProjectManagement.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KhoiProjectManagementApi.Controllers
{
    // Generic hierarchical-container CRUD, reused by the vault (categories are just Spaces) and every
    // future Space-scoped module (wiki pages, file libraries, etc.) - not a vault-specific API.
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SpacesController : ControllerBase
    {
        private readonly ISpaceService _spaceService;
        private readonly IAuthorizationService _authorizationService;

        public SpacesController(ISpaceService spaceService, IAuthorizationService authorizationService)
        {
            _spaceService = spaceService;
            _authorizationService = authorizationService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<SpaceDto>>> GetSpaces([FromQuery] int? parentSpaceId)
        {
            var spaces = await _spaceService.GetSpacesAsync(parentSpaceId, User);
            return Ok(spaces);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<SpaceDto>> GetSpace(int id)
        {
            var space = await _spaceService.GetSpaceByIdAsync(id, User);
            if (space == null)
                return NotFound();

            return Ok(space);
        }

        [HttpPost]
        public async Task<ActionResult<SpaceDto>> CreateSpace(CreateSpaceDto dto)
        {
            // A root Space (no parent) is an administrative action; a child Space requires Manage on
            // its parent - both resolved through the same Space-scoped authorization as everything else.
            if (dto.ParentSpaceId.HasValue)
            {
                var authResult = await _authorizationService.AuthorizeAsync(
                    User, new SpaceReference(dto.ParentSpaceId.Value), new SpacePermissionRequirement(PermissionLevel.Manage));
                if (!authResult.Succeeded)
                    return Forbid();
            }
            else if (!User.HasClaim("permission", "spaces.manage"))
            {
                return Forbid();
            }

            try
            {
                var space = await _spaceService.CreateSpaceAsync(dto, GetUserId());
                return CreatedAtAction(nameof(GetSpace), new { id = space.Id }, space);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateSpace(int id, UpdateSpaceDto dto)
        {
            if (!await HasManageAccessAsync(id))
                return Forbid();

            var updated = await _spaceService.UpdateSpaceAsync(id, dto);
            if (!updated)
                return NotFound();

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSpace(int id)
        {
            if (!await HasManageAccessAsync(id))
                return Forbid();

            try
            {
                var deleted = await _spaceService.DeleteSpaceAsync(id);
                if (!deleted)
                    return NotFound();

                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("{id}/permissions")]
        public async Task<ActionResult<IEnumerable<SpacePermissionDto>>> GetPermissions(int id)
        {
            if (!await HasManageAccessAsync(id))
                return Forbid();

            var permissions = await _spaceService.GetSpacePermissionsAsync(id);
            return Ok(permissions);
        }

        // Deliberately no Manage check, unlike GetPermissions above - a bare count doesn't reveal who
        // specifically has access, only how many people do, so anyone who can already see this Space
        // (enforced by SpaceTree only ever listing Spaces the caller has at least Read on) can see it.
        [HttpGet("{id}/grantee-count")]
        public async Task<ActionResult<int>> GetGranteeCount(int id)
        {
            return Ok(await _spaceService.GetSpaceGranteeCountAsync(id));
        }

        [HttpPut("{id}/permissions")]
        public async Task<IActionResult> SetPermissions(int id, List<SetSpacePermissionDto> grants)
        {
            if (!await HasManageAccessAsync(id))
                return Forbid();

            try
            {
                var updated = await _spaceService.SetSpacePermissionsAsync(id, grants, GetUserId());
                if (!updated)
                    return NotFound();

                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        private async Task<bool> HasManageAccessAsync(int spaceId)
        {
            var result = await _authorizationService.AuthorizeAsync(User, new SpaceReference(spaceId), new SpacePermissionRequirement(PermissionLevel.Manage));
            return result.Succeeded;
        }

        private int GetUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)!;
            return int.Parse(claim.Value);
        }
    }
}
