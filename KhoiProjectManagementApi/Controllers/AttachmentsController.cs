using KhoiProjectManagement.Application;
using KhoiProjectManagement.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace KhoiProjectManagementApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AttachmentsController : ControllerBase
    {
        private readonly IAttachmentService _attachmentService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AttachmentsController> _logger;

        public AttachmentsController(IAttachmentService attachmentService, IConfiguration configuration, ILogger<AttachmentsController> logger)
        {
            _attachmentService = attachmentService;
            _configuration = configuration;
            _logger = logger;
        }

        [HttpPost("upload")]
        public async Task<ActionResult<AttachmentDto>> UploadFile(IFormFile file, [FromForm] int? projectId, [FromForm] int? taskId)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file provided");

            var maxFileSize = long.Parse(_configuration["FileUpload:MaxFileSize"] ?? "10485760");
            if (file.Length > maxFileSize)
                return BadRequest("File size exceeds maximum allowed size");

            var allowedExtensions = _configuration.GetSection("FileUpload:AllowedExtensions").Get<string[]>() ?? Array.Empty<string>();
            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(fileExtension))
                return BadRequest("File type not allowed");

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
                return Unauthorized();

            var userId = int.Parse(userIdClaim.Value);

            try
            {
                var attachment = await _attachmentService.UploadFileAsync(file, userId, projectId, taskId);
                return Ok(attachment);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("{id}/download")]
        public async Task<IActionResult> DownloadFile(int id)
        {
            var attachment = await _attachmentService.GetAttachmentByIdAsync(id);
            if (attachment == null)
                return NotFound("Attachment not found");

            var uploadPath = _configuration["FileUpload:UploadPath"] ?? Path.Combine("wwwroot", "uploads");
            var filePath = Path.Combine(uploadPath, attachment.FileName ?? string.Empty);

            if (!System.IO.File.Exists(filePath))
                return NotFound("File not found on disk");

            try
            {
                var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
                return File(fileBytes, attachment.ContentType, attachment.FileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read attachment {AttachmentId} from disk at {FilePath}", id, filePath);
                return StatusCode(500, "An error occurred while reading the file.");
            }
        }


        [HttpDelete("{id}")]
        [Authorize(Policy = "attachments.delete")]
        public async Task<IActionResult> DeleteFile(int id)
        {
            var deleted = await _attachmentService.DeleteAttachmentAsync(id);
            if (!deleted)
                return NotFound();

            return NoContent();
        }
    }
}
