using KhoiProjectManagement.Application;
using KhoiProjectManagement.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KhoiProjectManagementApi.Controllers
{
    // Narrowed to VaultEntry CRUD + reveal/audit only - category (Space) management goes through the
    // generic SpacesController, proving reuse rather than a vault-specific fork. Every action here
    // re-checks the caller's Space-scoped permission inside VaultService, not just this [Authorize].
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class VaultController : ControllerBase
    {
        private readonly IVaultService _vaultService;

        public VaultController(IVaultService vaultService)
        {
            _vaultService = vaultService;
        }

        [HttpGet("entries")]
        public async Task<ActionResult<IEnumerable<VaultEntrySummaryDto>>> GetEntries([FromQuery] int spaceId)
        {
            try
            {
                var entries = await _vaultService.GetEntriesAsync(spaceId, User);
                return Ok(entries);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        [HttpGet("entries/{id}")]
        public async Task<ActionResult<VaultEntryDetailDto>> GetEntry(int id)
        {
            try
            {
                var entry = await _vaultService.GetEntryByIdAsync(id, User);
                if (entry == null)
                    return NotFound();

                return Ok(entry);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        [HttpPost("entries/{id}/reveal")]
        public async Task<ActionResult<VaultSecretRevealDto>> RevealSecret(int id)
        {
            try
            {
                var secret = await _vaultService.RevealSecretAsync(id, User);
                if (secret == null)
                    return NotFound();

                return Ok(secret);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        [HttpPost("entries")]
        public async Task<ActionResult<VaultEntryDetailDto>> CreateEntry(CreateVaultEntryDto dto)
        {
            try
            {
                var entry = await _vaultService.CreateEntryAsync(dto, User);
                return CreatedAtAction(nameof(GetEntry), new { id = entry.Id }, entry);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        [HttpPost("entries/import")]
        public async Task<ActionResult<VaultImportResultDto>> ImportEntries([FromQuery] int spaceId, IFormFile file)
        {
            try
            {
                var result = await _vaultService.ImportEntriesAsync(spaceId, file, User);
                return Ok(result);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("entries/{id}")]
        public async Task<IActionResult> UpdateEntry(int id, UpdateVaultEntryDto dto)
        {
            try
            {
                var updated = await _vaultService.UpdateEntryAsync(id, dto, User);
                if (!updated)
                    return NotFound();

                return NoContent();
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        [HttpDelete("entries/{id}")]
        public async Task<IActionResult> DeleteEntry(int id)
        {
            try
            {
                var deleted = await _vaultService.DeleteEntryAsync(id, User);
                if (!deleted)
                    return NotFound();

                return NoContent();
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        [HttpGet("entries/{id}/audit")]
        public async Task<ActionResult<IEnumerable<VaultAuditLogDto>>> GetAuditLog(int id)
        {
            try
            {
                var log = await _vaultService.GetAuditLogAsync(id, User);
                if (log == null)
                    return NotFound();

                return Ok(log);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }
    }
}
