using KhoiProjectManagement.Models.DTOs;
using KhoiProjectManagementApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KhoiProjectManagementApi.Controllers
{
    // Flat, record-oriented HR onboarding - a checklist belongs to exactly one User, no Space needed
    // (see plan Phase 7). Template management is hr.manage only; checklist access is hr.view/hr.manage
    // or the checklist's own owner, checked inside HrService since a blanket [Authorize] can't see
    // whose checklist a given id belongs to.
    [ApiController]
    [Route("api/hr")]
    [Authorize]
    public class HrController : ControllerBase
    {
        private readonly IHrService _hrService;

        public HrController(IHrService hrService)
        {
            _hrService = hrService;
        }

        [HttpGet("templates")]
        [Authorize(Policy = "hr.manage")]
        public async Task<IActionResult> GetTemplates()
        {
            return Ok(await _hrService.GetTemplatesAsync());
        }

        [HttpPost("templates")]
        [Authorize(Policy = "hr.manage")]
        public async Task<IActionResult> CreateTemplate(CreateOnboardingTemplateDto dto)
        {
            var template = await _hrService.CreateTemplateAsync(dto);
            return CreatedAtAction(nameof(GetTemplates), template);
        }

        [HttpPut("templates/{id}")]
        [Authorize(Policy = "hr.manage")]
        public async Task<IActionResult> UpdateTemplate(int id, UpdateOnboardingTemplateDto dto)
        {
            var updated = await _hrService.UpdateTemplateAsync(id, dto);
            if (!updated)
                return NotFound();

            return NoContent();
        }

        [HttpGet("checklists")]
        public async Task<IActionResult> GetChecklists([FromQuery] int? userId)
        {
            try
            {
                return Ok(await _hrService.GetChecklistsAsync(userId, User));
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        [HttpGet("checklists/{id}")]
        public async Task<IActionResult> GetChecklist(int id)
        {
            try
            {
                var checklist = await _hrService.GetChecklistByIdAsync(id, User);
                if (checklist == null)
                    return NotFound();

                return Ok(checklist);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        [HttpPost("checklists")]
        [Authorize(Policy = "hr.manage")]
        public async Task<IActionResult> CreateChecklist(CreateOnboardingChecklistDto dto)
        {
            try
            {
                var checklist = await _hrService.CreateChecklistAsync(dto);
                return CreatedAtAction(nameof(GetChecklist), new { id = checklist.Id }, checklist);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("checklists/{id}/items/{itemId}")]
        public async Task<IActionResult> UpdateChecklistItem(int id, int itemId, UpdateChecklistItemDto dto)
        {
            try
            {
                var updated = await _hrService.UpdateChecklistItemAsync(id, itemId, dto, User);
                if (!updated)
                    return NotFound();

                return NoContent();
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }
    }
}
