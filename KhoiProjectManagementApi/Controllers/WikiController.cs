using KhoiProjectManagement.Application;
using KhoiProjectManagement.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KhoiProjectManagementApi.Controllers
{
    // Confluence-style wiki pages - the second Space-scoped consumer after the vault. Every action
    // re-checks the caller's Space-scoped permission inside WikiService, not just this [Authorize],
    // via the same SpacePermissionResolver/SpacePermissionAuthorizationHandler the vault uses.
    [ApiController]
    [Route("api/wiki")]
    [Authorize]
    public class WikiController : ControllerBase
    {
        private readonly IWikiService _wikiService;

        public WikiController(IWikiService wikiService)
        {
            _wikiService = wikiService;
        }

        [HttpGet("search")]
        public async Task<ActionResult<IEnumerable<WikiSearchResultDto>>> Search([FromQuery] string q)
        {
            return Ok(await _wikiService.SearchPagesAsync(q, User));
        }

        [HttpGet("pages")]
        public async Task<ActionResult<IEnumerable<WikiPageSummaryDto>>> GetPages([FromQuery] int spaceId, [FromQuery] int? parentPageId)
        {
            try
            {
                var pages = await _wikiService.GetPagesAsync(spaceId, parentPageId, User);
                return Ok(pages);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        [HttpGet("pages/{id:int}")]
        public async Task<ActionResult<WikiPageDetailDto>> GetPage(int id)
        {
            try
            {
                var page = await _wikiService.GetPageByIdAsync(id, User);
                if (page == null)
                    return NotFound();

                return Ok(page);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        [HttpPost("pages")]
        public async Task<ActionResult<WikiPageDetailDto>> CreatePage(CreateWikiPageDto dto)
        {
            try
            {
                var page = await _wikiService.CreatePageAsync(dto, User);
                return CreatedAtAction(nameof(GetPage), new { id = page.Id }, page);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        [HttpPut("pages/{id:int}")]
        public async Task<IActionResult> UpdatePage(int id, UpdateWikiPageDto dto)
        {
            try
            {
                var updated = await _wikiService.UpdatePageAsync(id, dto, User);
                if (!updated)
                    return NotFound();

                return NoContent();
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        [HttpDelete("pages/{id:int}")]
        public async Task<IActionResult> DeletePage(int id)
        {
            try
            {
                var deleted = await _wikiService.DeletePageAsync(id, User);
                if (!deleted)
                    return NotFound();

                return NoContent();
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        [HttpPut("pages/{id}/move")]
        public async Task<IActionResult> MovePage(int id, MoveWikiPageDto dto)
        {
            try
            {
                var moved = await _wikiService.MovePageAsync(id, dto, User);
                if (!moved)
                    return NotFound();

                return NoContent();
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

        [HttpPut("pages/reorder")]
        public async Task<IActionResult> ReorderPages([FromQuery] int spaceId, [FromQuery] int? parentPageId, ReorderWikiPagesDto dto)
        {
            try
            {
                var reordered = await _wikiService.ReorderPagesAsync(spaceId, parentPageId, dto, User);
                if (!reordered)
                    return NotFound();

                return NoContent();
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

        [HttpPut("pages/{id}/labels")]
        public async Task<IActionResult> SetLabels(int id, SetWikiPageLabelsDto dto)
        {
            try
            {
                var updated = await _wikiService.SetLabelsAsync(id, dto, User);
                if (!updated)
                    return NotFound();

                return NoContent();
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        [HttpGet("pages/{id}/versions")]
        public async Task<ActionResult<IEnumerable<WikiPageVersionSummaryDto>>> GetVersions(int id)
        {
            try
            {
                var versions = await _wikiService.GetVersionsAsync(id, User);
                if (versions == null)
                    return NotFound();

                return Ok(versions);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        [HttpGet("pages/{id}/versions/{versionNumber}")]
        public async Task<ActionResult<WikiPageVersionDetailDto>> GetVersion(int id, int versionNumber)
        {
            try
            {
                var version = await _wikiService.GetVersionAsync(id, versionNumber, User);
                if (version == null)
                    return NotFound();

                return Ok(version);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        [HttpGet("pages/{id}/comments")]
        public async Task<ActionResult<IEnumerable<WikiCommentDto>>> GetComments(int id)
        {
            try
            {
                var comments = await _wikiService.GetCommentsAsync(id, User);
                if (comments == null)
                    return NotFound();

                return Ok(comments);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        [HttpPost("pages/{id}/comments")]
        public async Task<ActionResult<WikiCommentDto>> AddComment(int id, CreateWikiCommentDto dto)
        {
            try
            {
                var comment = await _wikiService.AddCommentAsync(id, dto, User);
                return Ok(comment);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        [HttpDelete("comments/{commentId}")]
        public async Task<IActionResult> DeleteComment(int commentId)
        {
            try
            {
                var deleted = await _wikiService.DeleteCommentAsync(commentId, User);
                if (!deleted)
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
