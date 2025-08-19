using KhoiProjectManagement.Models.DTOs;

namespace KhoiProjectManagementApi.Services
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
