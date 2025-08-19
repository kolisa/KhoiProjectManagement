using KhoiProjectManagement.Models;
using KhoiProjectManagement.Models.DTOs;
using KhoiProjectManagementApi.Data;
using Microsoft.EntityFrameworkCore;

namespace KhoiProjectManagementApi.Services
{
    public class AttachmentService : IAttachmentService
    {
        private readonly ProjectManagementContext _context;
        private readonly IConfiguration _configuration;

        public AttachmentService(ProjectManagementContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public async Task<AttachmentDto> UploadFileAsync(IFormFile file, int uploadedBy, int? projectId, int? taskId)
        {
            if (projectId == null && taskId == null)
                throw new InvalidOperationException("Either projectId or taskId must be provided");

            if (projectId != null && taskId != null)
                throw new InvalidOperationException("Cannot attach to both project and task");

            var uploadPath = _configuration["FileUpload:UploadPath"] ?? "wwwroot/uploads";
            var fileName = $"{Guid.NewGuid()}_{file.FileName}";
            var filePath = Path.Combine(uploadPath, fileName);

            // Ensure directory exists
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

            // Save file to disk
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var attachment = new Attachment
            {
                FileName = file.FileName,
                FilePath = fileName,
                FileSize = file.Length,
                ContentType = file.ContentType,
                ProjectId = projectId,
                TaskId = taskId,
                UploadedBy = uploadedBy
            };

            _context.Attachments.Add(attachment);
            await _context.SaveChangesAsync();

            return MapToDto(attachment);
        }

        public async Task<AttachmentDto?> GetAttachmentByIdAsync(int id)
        {
            var attachment = await _context.Attachments
                .Include(a => a.UploadedByUser)
                .FirstOrDefaultAsync(a => a.Id == id);

            return attachment == null ? null : MapToDto(attachment);
        }

        public async Task<IEnumerable<AttachmentDto>> GetProjectAttachmentsAsync(int projectId)
        {
            var attachments = await _context.Attachments
                .Include(a => a.UploadedByUser)
                .Where(a => a.ProjectId == projectId)
                .ToListAsync();

            return attachments.Select(MapToDto);
        }

        public async Task<IEnumerable<AttachmentDto>> GetTaskAttachmentsAsync(int taskId)
        {
            var attachments = await _context.Attachments
                .Include(a => a.UploadedByUser)
                .Where(a => a.TaskId == taskId)
                .ToListAsync();

            return attachments.Select(MapToDto);
        }

        public async Task<bool> DeleteAttachmentAsync(int id)
        {
            var attachment = await _context.Attachments.FindAsync(id);
            if (attachment == null)
                return false;

            // Delete file from disk
            var uploadPath = _configuration["FileUpload:UploadPath"] ?? "wwwroot/uploads";
            var filePath = Path.Combine(uploadPath, attachment.FilePath);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            _context.Attachments.Remove(attachment);
            await _context.SaveChangesAsync();
            return true;
        }

        private static AttachmentDto MapToDto(Attachment attachment)
        {
            return new AttachmentDto
            {
                Id = attachment.Id,
                FileName = attachment.FileName,
                FileSize = attachment.FileSize,
                ContentType = attachment.ContentType,
                ProjectId = attachment.ProjectId,
                TaskId = attachment.TaskId,
                UploadedBy = attachment.UploadedByUser?.Name ?? "Unknown",
                UploadedAt = attachment.UploadedAt
            };
        }
    }
}
