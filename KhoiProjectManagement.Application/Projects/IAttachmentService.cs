using KhoiProjectManagement.Application;

using Microsoft.AspNetCore.Http;
namespace KhoiProjectManagement.Application
{
    public interface IAttachmentService
    {
        Task<AttachmentDto> UploadFileAsync(IFormFile file, int uploadedBy, int? projectId, int? taskId);
        Task<AttachmentDto?> GetAttachmentByIdAsync(int id);
        Task<IEnumerable<AttachmentDto>> GetProjectAttachmentsAsync(int projectId);
        Task<IEnumerable<AttachmentDto>> GetTaskAttachmentsAsync(int taskId);
        Task<bool> DeleteAttachmentAsync(int id);
    }
}
