using System.Security.Claims;
using KhoiProjectManagement.Models.DTOs;
using KhoiProjectManagementApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;

namespace KhoiProjectManagementApi.Controllers
{
    // Company-wide, flat ideas board - everyone can submit/comment with no permission needed;
    // ideas.manage gates moving Status, deleting others' comments, and converting to a project
    // (see plan Phase 11).
    [ApiController]
    [Route("api/ideas")]
    [Authorize]
    public class IdeasController : ControllerBase
    {
        private readonly IIdeaService _ideaService;

        public IdeasController(IIdeaService ideaService)
        {
            _ideaService = ideaService;
        }

        [HttpGet]
        public async Task<IActionResult> GetIdeas([FromQuery] string? status)
        {
            return Ok(await _ideaService.GetIdeasAsync(status));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetIdea(int id)
        {
            var idea = await _ideaService.GetIdeaByIdAsync(id);
            if (idea == null)
                return NotFound();

            return Ok(idea);
        }

        [HttpPost]
        public async Task<IActionResult> CreateIdea(CreateIdeaDto dto)
        {
            var idea = await _ideaService.CreateIdeaAsync(dto, User);
            return CreatedAtAction(nameof(GetIdea), new { id = idea.Id }, idea);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateIdea(int id, UpdateIdeaDto dto)
        {
            try
            {
                var updated = await _ideaService.UpdateIdeaAsync(id, dto, User);
                if (!updated)
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

        [HttpPut("{id}/status")]
        [Authorize(Policy = "ideas.manage")]
        public async Task<IActionResult> UpdateStatus(int id, IdeaStatusUpdateDto dto)
        {
            try
            {
                var updated = await _ideaService.UpdateStatusAsync(id, dto.Status);
                if (!updated)
                    return NotFound();

                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("{id}/convert-to-project")]
        [Authorize(Policy = "ideas.manage")]
        public async Task<IActionResult> ConvertToProject(int id)
        {
            try
            {
                var idea = await _ideaService.ConvertToProjectAsync(id, GetUserId());
                if (idea == null)
                    return NotFound();

                return Ok(idea);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("{id}/comments")]
        public async Task<IActionResult> GetComments(int id)
        {
            var comments = await _ideaService.GetCommentsAsync(id);
            if (comments == null)
                return NotFound();

            return Ok(comments);
        }

        [HttpPost("{id}/comments")]
        public async Task<IActionResult> AddComment(int id, CreateIdeaCommentDto dto)
        {
            var comment = await _ideaService.AddCommentAsync(id, dto, User);
            if (comment == null)
                return NotFound();

            return CreatedAtAction(nameof(GetComments), new { id }, comment);
        }

        [HttpDelete("comments/{commentId}")]
        public async Task<IActionResult> DeleteComment(int commentId)
        {
            try
            {
                var deleted = await _ideaService.DeleteCommentAsync(commentId, User);
                if (!deleted)
                    return NotFound();

                return NoContent();
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        [HttpGet("{id}/attachments")]
        public async Task<IActionResult> GetAttachments(int id)
        {
            var attachments = await _ideaService.GetAttachmentsAsync(id);
            if (attachments == null)
                return NotFound();

            return Ok(attachments);
        }

        [HttpPost("{id}/attachments")]
        public async Task<IActionResult> UploadAttachment(int id, [FromForm] IFormFile file)
        {
            var attachment = await _ideaService.UploadAttachmentAsync(id, file, User);
            if (attachment == null)
                return NotFound();

            return CreatedAtAction(nameof(GetAttachments), new { id }, attachment);
        }

        [HttpGet("attachments/{attachmentId}/download")]
        public async Task<IActionResult> DownloadAttachment(int attachmentId)
        {
            var result = await _ideaService.DownloadAttachmentAsync(attachmentId);
            if (result == null)
                return NotFound();

            return File(result.Value.Content, result.Value.ContentType, result.Value.FileName);
        }

        [HttpDelete("attachments/{attachmentId}")]
        public async Task<IActionResult> DeleteAttachment(int attachmentId)
        {
            try
            {
                var deleted = await _ideaService.DeleteAttachmentAsync(attachmentId, User);
                if (!deleted)
                    return NotFound();

                return NoContent();
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        [HttpGet("attachments/{attachmentId}/annotations")]
        public async Task<IActionResult> GetAnnotations(int attachmentId)
        {
            var annotations = await _ideaService.GetAnnotationsAsync(attachmentId);
            if (annotations == null)
                return NotFound();

            return Ok(annotations);
        }

        [HttpPost("attachments/{attachmentId}/annotations")]
        public async Task<IActionResult> AddAnnotation(int attachmentId, CreateIdeaAttachmentAnnotationDto dto)
        {
            var annotation = await _ideaService.AddAnnotationAsync(attachmentId, dto, User);
            if (annotation == null)
                return NotFound();

            return CreatedAtAction(nameof(GetAnnotations), new { attachmentId }, annotation);
        }

        [HttpDelete("annotations/{annotationId}")]
        public async Task<IActionResult> DeleteAnnotation(int annotationId)
        {
            try
            {
                var deleted = await _ideaService.DeleteAnnotationAsync(annotationId, User);
                if (!deleted)
                    return NotFound();

                return NoContent();
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        private int GetUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)!;
            return int.Parse(claim.Value);
        }
    }
}
