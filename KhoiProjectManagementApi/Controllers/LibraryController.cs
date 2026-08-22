using KhoiProjectManagementApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KhoiProjectManagementApi.Controllers
{
    // SharePoint-style file library, third ISpaceScoped consumer alongside Vault and Wiki - same
    // Controller -> Service -> Context pattern, Read/Write/Manage on the file's Space, zero new
    // authorization code.
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class LibraryController : ControllerBase
    {
        private readonly ILibraryService _libraryService;

        public LibraryController(ILibraryService libraryService)
        {
            _libraryService = libraryService;
        }

        [HttpGet("files")]
        public async Task<IActionResult> GetFiles([FromQuery] int spaceId)
        {
            try
            {
                var files = await _libraryService.GetFilesAsync(spaceId, User);
                return Ok(files);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        [HttpGet("files/{id}")]
        public async Task<IActionResult> GetFile(int id)
        {
            try
            {
                var file = await _libraryService.GetFileByIdAsync(id, User);
                if (file == null)
                    return NotFound();

                return Ok(file);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        [HttpGet("files/{id}/download")]
        public async Task<IActionResult> DownloadFile(int id)
        {
            try
            {
                var result = await _libraryService.DownloadCurrentAsync(id, User);
                if (result == null)
                    return NotFound();

                return File(result.Value.Content, result.Value.ContentType, result.Value.FileName);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        [HttpPost("files")]
        public async Task<IActionResult> UploadFile([FromForm] int spaceId, [FromForm] IFormFile file)
        {
            try
            {
                var created = await _libraryService.UploadNewFileAsync(spaceId, file, User);
                return CreatedAtAction(nameof(GetFile), new { id = created.Id }, created);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        [HttpPost("files/{id}/versions")]
        public async Task<IActionResult> UploadNewVersion(int id, [FromForm] IFormFile file, [FromForm] string? comment)
        {
            try
            {
                var uploaded = await _libraryService.UploadNewVersionAsync(id, file, comment, User);
                if (!uploaded)
                    return NotFound();

                return NoContent();
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        [HttpGet("files/{id}/versions")]
        public async Task<IActionResult> GetVersions(int id)
        {
            try
            {
                var versions = await _libraryService.GetVersionsAsync(id, User);
                if (versions == null)
                    return NotFound();

                return Ok(versions);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        [HttpGet("files/{id}/versions/{versionNumber}/download")]
        public async Task<IActionResult> DownloadVersion(int id, int versionNumber)
        {
            try
            {
                var result = await _libraryService.DownloadVersionAsync(id, versionNumber, User);
                if (result == null)
                    return NotFound();

                return File(result.Value.Content, result.Value.ContentType, result.Value.FileName);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        [HttpDelete("files/{id}")]
        public async Task<IActionResult> DeleteFile(int id)
        {
            try
            {
                var deleted = await _libraryService.DeleteFileAsync(id, User);
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
