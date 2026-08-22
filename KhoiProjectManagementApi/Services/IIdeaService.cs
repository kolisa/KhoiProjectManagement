using System.Security.Claims;
using KhoiProjectManagement.Models.DTOs;
using Microsoft.AspNetCore.Http;

namespace KhoiProjectManagementApi.Services
{
    public interface IIdeaService
    {
        Task<List<IdeaDto>> GetIdeasAsync(string? status);
        Task<IdeaDto?> GetIdeaByIdAsync(int id);
        Task<IdeaDto> CreateIdeaAsync(CreateIdeaDto dto, ClaimsPrincipal caller);
        Task<bool> UpdateIdeaAsync(int id, UpdateIdeaDto dto, ClaimsPrincipal caller);
        Task<bool> UpdateStatusAsync(int id, string status);
        Task<IdeaDto?> ConvertToProjectAsync(int id, int callerId);

        Task<List<IdeaCommentDto>?> GetCommentsAsync(int ideaId);
        Task<IdeaCommentDto?> AddCommentAsync(int ideaId, CreateIdeaCommentDto dto, ClaimsPrincipal caller);
        Task<bool> DeleteCommentAsync(int commentId, ClaimsPrincipal caller);

        // Prototype/mockup file attachments - everyone can upload/view, matching Ideas' company-wide,
        // flat access model; delete requires the uploader or ideas.manage.
        Task<List<IdeaAttachmentDto>?> GetAttachmentsAsync(int ideaId);
        Task<IdeaAttachmentDto?> UploadAttachmentAsync(int ideaId, IFormFile file, ClaimsPrincipal caller);
        Task<(byte[] Content, string ContentType, string FileName)?> DownloadAttachmentAsync(int attachmentId);
        Task<bool> DeleteAttachmentAsync(int attachmentId, ClaimsPrincipal caller);

        // Short notes tied to one specific attachment - distinct from the idea-level comment thread.
        Task<List<IdeaAttachmentAnnotationDto>?> GetAnnotationsAsync(int attachmentId);
        Task<IdeaAttachmentAnnotationDto?> AddAnnotationAsync(int attachmentId, CreateIdeaAttachmentAnnotationDto dto, ClaimsPrincipal caller);
        Task<bool> DeleteAnnotationAsync(int annotationId, ClaimsPrincipal caller);
    }
}
