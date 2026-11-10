using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using KhoiProjectManagement.Application;
using Microsoft.EntityFrameworkCore;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
namespace KhoiProjectManagement.Application
{
    public class AttachmentService : IAttachmentService
    {
        private readonly IRepository<Attachment> _attachmentRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IConfiguration _configuration;
        private readonly IRepository<ProjectTask> _taskRepo;
        private readonly IProjectScope _scope;

        public AttachmentService(IRepository<Attachment> attachmentRepo, IUnitOfWork unitOfWork, IConfiguration configuration,
            IRepository<ProjectTask> taskRepo, IProjectScope scope)
        {
            _taskRepo = taskRepo;
            _scope = scope;
            _attachmentRepo = attachmentRepo;
            _unitOfWork = unitOfWork;
            _configuration = configuration;
        }

        public async Task<AttachmentDto> UploadFileAsync(IFormFile file, int uploadedBy, int? projectId, int? taskId)
        {
            if (projectId == null && taskId == null)
                throw new InvalidOperationException("Either projectId or taskId must be provided");

            if (projectId != null && taskId != null)
                throw new InvalidOperationException("Cannot attach to both project and task");

            // A file may only go into a project the caller is allowed to change work in.
            int? owningProjectId = projectId ?? await _taskRepo.Query().AsNoTracking().Where(t => t.Id == taskId)
                .Select(t => (int?)t.ProjectId).FirstOrDefaultAsync();
            if (owningProjectId == null)
                throw new KeyNotFoundException();
            await _scope.RequireAsync(owningProjectId.Value, ProjectAbility.Edit);

            var uploadPath = _configuration["FileUpload:UploadPath"] ?? "wwwroot/uploads";
            var fileName = UploadFileNaming.BuildStoredFileName(file.FileName);
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
                FileName = UploadFileNaming.SanitizeFileName(file.FileName),
                FilePath = fileName,
                FileSize = file.Length,
                ContentType = file.ContentType,
                ProjectId = projectId,
                TaskId = taskId,
                UploadedBy = uploadedBy
            };

            _attachmentRepo.Add(attachment);
            await _unitOfWork.SaveChangesAsync();

            return MapToDto(attachment);
        }

        public async Task<AttachmentDto?> GetAttachmentByIdAsync(int id)
        {
            var attachment = await _attachmentRepo.Query()
                .Include(a => a.UploadedByUser)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (attachment == null || await CheckAsync(attachment, ProjectAbility.View) != ProjectAccessOutcome.Allowed)
                return null;   // an attachment in someone else's project does not exist here

            return MapToDto(attachment);
        }

        public async Task<IEnumerable<AttachmentDto>> GetProjectAttachmentsAsync(int projectId)
        {
            if (await _scope.CheckAsync(projectId, ProjectAbility.View) != ProjectAccessOutcome.Allowed)
                return Array.Empty<AttachmentDto>();

            var attachments = await _attachmentRepo.Query()
                .Include(a => a.UploadedByUser)
                .Where(a => a.ProjectId == projectId)
                .ToListAsync();

            return attachments.Select(MapToDto);
        }

        public async Task<IEnumerable<AttachmentDto>> GetTaskAttachmentsAsync(int taskId)
        {
            var owner = await _taskRepo.Query().AsNoTracking().Where(t => t.Id == taskId).Select(t => (int?)t.ProjectId).FirstOrDefaultAsync();
            if (owner == null || await _scope.CheckAsync(owner.Value, ProjectAbility.View) != ProjectAccessOutcome.Allowed)
                return Array.Empty<AttachmentDto>();

            var attachments = await _attachmentRepo.Query()
                .Include(a => a.UploadedByUser)
                .Where(a => a.TaskId == taskId)
                .ToListAsync();

            return attachments.Select(MapToDto);
        }

        // Reads by FilePath (the actual GUID-prefixed on-disk name), never by the display FileName -
        // the two used to be conflated in AttachmentsController.DownloadFile, which built the disk path
        // straight from the display name and so could never actually find the file it just uploaded.
        public async Task<(byte[] Content, string ContentType, string FileName)?> DownloadFileAsync(int id)
        {
            var attachment = await _attachmentRepo.FindAsync(id);
            if (attachment == null || await CheckAsync(attachment, ProjectAbility.View) != ProjectAccessOutcome.Allowed)
                return null;

            var uploadPath = _configuration["FileUpload:UploadPath"] ?? "wwwroot/uploads";
            var filePath = Path.Combine(uploadPath, attachment.FilePath);
            if (!File.Exists(filePath))
                return null;

            var content = await File.ReadAllBytesAsync(filePath);
            return (content, attachment.ContentType, attachment.FileName);
        }

        public async Task<bool> DeleteAttachmentAsync(int id)
        {
            var attachment = await _attachmentRepo.FindAsync(id);
            if (attachment == null)
                return false;
            var outcome = await CheckAsync(attachment, ProjectAbility.Edit);
            if (outcome == ProjectAccessOutcome.NotVisible)
                return false;
            if (outcome == ProjectAccessOutcome.Forbidden)
                throw new ForbiddenException("Your role or access level on this project does not allow deleting its files.");

            // Delete file from disk
            var uploadPath = _configuration["FileUpload:UploadPath"] ?? "wwwroot/uploads";
            var filePath = Path.Combine(uploadPath, attachment.FilePath);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            _attachmentRepo.Remove(attachment);
            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        // The project an attachment belongs to (directly, or through its task) decides who may see or change it.
        private async Task<ProjectAccessOutcome> CheckAsync(Attachment attachment, ProjectAbility ability)
        {
            int? owner = attachment.ProjectId ?? (attachment.TaskId == null ? null : await _taskRepo.Query().AsNoTracking()
                .Where(t => t.Id == attachment.TaskId).Select(t => (int?)t.ProjectId).FirstOrDefaultAsync());
            if (owner == null)
                return await _scope.IsUnrestrictedAsync() ? ProjectAccessOutcome.Allowed : ProjectAccessOutcome.NotVisible;
            return await _scope.CheckAsync(owner.Value, ability);
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
