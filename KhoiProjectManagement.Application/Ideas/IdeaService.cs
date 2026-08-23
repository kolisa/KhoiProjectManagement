using System.Security.Claims;
using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using KhoiProjectManagement.Application;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

using Microsoft.Extensions.Configuration;
namespace KhoiProjectManagement.Application
{
    public class IdeaService : IIdeaService
    {
        private static readonly string[] ValidStatuses = { "Submitted", "UnderReview", "Approved", "Rejected", "ConvertedToProject" };

        private readonly IRepository<Idea> _ideaRepo;
        private readonly IRepository<Project> _projectRepo;
        private readonly IRepository<IdeaComment> _commentRepo;
        private readonly IRepository<User> _userRepo;
        private readonly IRepository<IdeaAttachment> _attachmentRepo;
        private readonly IRepository<IdeaAttachmentAnnotation> _annotationRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly INotificationService _notificationService;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;

        public IdeaService(
            IRepository<Idea> ideaRepo,
            IRepository<Project> projectRepo,
            IRepository<IdeaComment> commentRepo,
            IRepository<User> userRepo,
            IRepository<IdeaAttachment> attachmentRepo,
            IRepository<IdeaAttachmentAnnotation> annotationRepo,
            IUnitOfWork unitOfWork,
            INotificationService notificationService,
            IEmailService emailService,
            IConfiguration configuration)
        {
            _ideaRepo = ideaRepo;
            _projectRepo = projectRepo;
            _commentRepo = commentRepo;
            _userRepo = userRepo;
            _attachmentRepo = attachmentRepo;
            _annotationRepo = annotationRepo;
            _unitOfWork = unitOfWork;
            _configuration = configuration;
            _notificationService = notificationService;
            _emailService = emailService;
        }

        public async Task<List<IdeaDto>> GetIdeasAsync(string? status)
        {
            var query = _ideaRepo.Query()
                .Include(i => i.Submitter)
                .Include(i => i.ConvertedProject)
                .Include(i => i.Comments)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status))
                query = query.Where(i => i.Status == status);

            var ideas = await query.OrderByDescending(i => i.CreatedAt).ToListAsync();
            return ideas.Select(MapToDto).ToList();
        }

        public async Task<IdeaDto?> GetIdeaByIdAsync(int id)
        {
            var idea = await LoadAsync(id);
            return idea == null ? null : MapToDto(idea);
        }

        public async Task<IdeaDto> CreateIdeaAsync(CreateIdeaDto dto, ClaimsPrincipal caller)
        {
            var idea = new Idea
            {
                Title = dto.Title,
                Description = dto.Description,
                SubmittedBy = GetUserId(caller)
            };

            _ideaRepo.Add(idea);
            await _unitOfWork.SaveChangesAsync();

            var saved = await LoadAsync(idea.Id);
            return MapToDto(saved!);
        }

        public async Task<bool> UpdateIdeaAsync(int id, UpdateIdeaDto dto, ClaimsPrincipal caller)
        {
            var idea = await _ideaRepo.Query().FirstOrDefaultAsync(i => i.Id == id);
            if (idea == null)
                return false;

            if (idea.SubmittedBy != GetUserId(caller))
                throw new UnauthorizedAccessException($"Caller lacks access to edit idea {id}.");

            if (idea.Status != "Submitted")
                throw new InvalidOperationException($"Cannot edit an idea with status '{idea.Status}'.");

            idea.Title = dto.Title;
            idea.Description = dto.Description;

            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<bool> UpdateStatusAsync(int id, string status)
        {
            if (!ValidStatuses.Contains(status) || status == "ConvertedToProject")
                throw new InvalidOperationException($"Invalid status '{status}'. Use the convert-to-project endpoint for ConvertedToProject.");

            var idea = await _ideaRepo.Query().FirstOrDefaultAsync(i => i.Id == id);
            if (idea == null)
                return false;

            idea.Status = status;
            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<IdeaDto?> ConvertToProjectAsync(int id, int callerId)
        {
            var idea = await _ideaRepo.Query().FirstOrDefaultAsync(i => i.Id == id);
            if (idea == null)
                return null;

            if (idea.Status == "ConvertedToProject")
                throw new InvalidOperationException("This idea has already been converted to a project.");

            // Deliberately construct the Project directly rather than going through
            // ProjectService.CreateProjectAsync, which hardcodes CreatedBy=1 (a pre-existing bug noted
            // but not fixed by this plan) - this path needs the real caller attributed correctly.
            var project = new Project
            {
                Name = idea.Title,
                Description = idea.Description,
                CreatedBy = callerId,
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddMonths(1)
            };

            _projectRepo.Add(project);
            await _unitOfWork.SaveChangesAsync(); // assigns project.Id

            idea.Status = "ConvertedToProject";
            idea.ConvertedProjectId = project.Id;
            await _unitOfWork.SaveChangesAsync();

            var saved = await LoadAsync(idea.Id);
            return MapToDto(saved!);
        }

        public async Task<List<IdeaCommentDto>?> GetCommentsAsync(int ideaId)
        {
            var ideaExists = await _ideaRepo.Query().AnyAsync(i => i.Id == ideaId);
            if (!ideaExists)
                return null;

            var comments = await _commentRepo.Query()
                .Include(c => c.Author)
                .Where(c => c.IdeaId == ideaId && !c.IsDeleted)
                .OrderBy(c => c.CreatedAt)
                .ToListAsync();

            return comments.Select(MapComment).ToList();
        }

        public async Task<IdeaCommentDto?> AddCommentAsync(int ideaId, CreateIdeaCommentDto dto, ClaimsPrincipal caller)
        {
            var idea = await _ideaRepo.Query().FirstOrDefaultAsync(i => i.Id == ideaId);
            if (idea == null)
                return null;

            var authorId = GetUserId(caller);
            var comment = new IdeaComment
            {
                IdeaId = ideaId,
                AuthoredBy = authorId,
                Body = dto.Body
            };

            _commentRepo.Add(comment);
            await _unitOfWork.SaveChangesAsync();

            var saved = await _commentRepo.Query().Include(c => c.Author).FirstAsync(c => c.Id == comment.Id);
            await NotifyMentionedUsersAsync(idea, comment, saved.Author?.Name ?? "Unknown", authorId);

            return MapComment(saved);
        }

        // Ideas are company-wide with no Space boundary (per plan Phase 11), so unlike wiki mentions
        // there's no access check needed - every active user can already see every idea.
        private async Task NotifyMentionedUsersAsync(Idea idea, IdeaComment comment, string authorName, int authorId)
        {
            var activeUsers = await _userRepo.Query()
                .Where(u => u.IsActive)
                .Select(u => new { u.Id, u.Name, u.Email })
                .ToListAsync();

            var mentionedIds = MentionParser.FindMentionedUserIds(
                comment.Body, activeUsers.Select(u => (u.Id, u.Name)), authorId);

            foreach (var mentionedId in mentionedIds)
            {
                await _notificationService.CreateNotificationAsync(
                    mentionedId,
                    "mention",
                    $"{authorName} mentioned you in a comment on idea '{idea.Title}'",
                    ideaId: idea.Id
                );

                if (await _notificationService.IsEmailEnabledAsync(mentionedId, NotificationTypes.Mention))
                {
                    var user = activeUsers.First(u => u.Id == mentionedId);
                    try
                    {
                        await _emailService.SendMentionEmailAsync(user.Email, authorName, "idea", idea.Title, comment.Body);
                    }
                    catch
                    {
                        // Comment already saved - a failed SMTP send must never fail the comment request.
                        // EmailService already records the failure to EmailLog before re-throwing.
                    }
                }
            }
        }

        public async Task<bool> DeleteCommentAsync(int commentId, ClaimsPrincipal caller)
        {
            var comment = await _commentRepo.Query().FirstOrDefaultAsync(c => c.Id == commentId);
            if (comment == null)
                return false;

            var callerId = GetUserId(caller);
            if (comment.AuthoredBy != callerId && !caller.HasClaim("permission", "ideas.manage"))
                throw new UnauthorizedAccessException($"Caller lacks access to delete comment {commentId}.");

            comment.IsDeleted = true;
            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<List<IdeaAttachmentDto>?> GetAttachmentsAsync(int ideaId)
        {
            var ideaExists = await _ideaRepo.Query().AnyAsync(i => i.Id == ideaId);
            if (!ideaExists)
                return null;

            var attachments = await _attachmentRepo.Query()
                .Include(a => a.Uploader)
                .Include(a => a.Annotations)
                .Where(a => a.IdeaId == ideaId)
                .OrderByDescending(a => a.UploadedAt)
                .ToListAsync();

            return attachments.Select(MapAttachment).ToList();
        }

        public async Task<IdeaAttachmentDto?> UploadAttachmentAsync(int ideaId, IFormFile file, ClaimsPrincipal caller)
        {
            var ideaExists = await _ideaRepo.Query().AnyAsync(i => i.Id == ideaId);
            if (!ideaExists)
                return null;

            var uploadPath = _configuration["FileUpload:IdeaPath"] ?? "wwwroot/idea-files";
            var storedFileName = $"{Guid.NewGuid()}_{file.FileName}";
            var filePath = Path.Combine(uploadPath, storedFileName);

            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var attachment = new IdeaAttachment
            {
                IdeaId = ideaId,
                OriginalFileName = file.FileName,
                StoredFileName = storedFileName,
                ContentType = file.ContentType,
                FileSize = file.Length,
                UploadedBy = GetUserId(caller)
            };

            _attachmentRepo.Add(attachment);
            await _unitOfWork.SaveChangesAsync();

            var saved = await _attachmentRepo.Query()
                .Include(a => a.Uploader)
                .Include(a => a.Annotations)
                .FirstAsync(a => a.Id == attachment.Id);

            return MapAttachment(saved);
        }

        public async Task<(byte[] Content, string ContentType, string FileName)?> DownloadAttachmentAsync(int attachmentId)
        {
            var attachment = await _attachmentRepo.Query().FirstOrDefaultAsync(a => a.Id == attachmentId);
            if (attachment == null)
                return null;

            var uploadPath = _configuration["FileUpload:IdeaPath"] ?? "wwwroot/idea-files";
            var filePath = Path.Combine(uploadPath, attachment.StoredFileName);
            if (!File.Exists(filePath))
                return null;

            var content = await File.ReadAllBytesAsync(filePath);
            return (content, attachment.ContentType, attachment.OriginalFileName);
        }

        public async Task<bool> DeleteAttachmentAsync(int attachmentId, ClaimsPrincipal caller)
        {
            var attachment = await _attachmentRepo.Query().FirstOrDefaultAsync(a => a.Id == attachmentId);
            if (attachment == null)
                return false;

            var callerId = GetUserId(caller);
            if (attachment.UploadedBy != callerId && !caller.HasClaim("permission", "ideas.manage"))
                throw new UnauthorizedAccessException($"Caller lacks access to delete attachment {attachmentId}.");

            var uploadPath = _configuration["FileUpload:IdeaPath"] ?? "wwwroot/idea-files";
            var filePath = Path.Combine(uploadPath, attachment.StoredFileName);
            if (File.Exists(filePath))
                File.Delete(filePath);

            _attachmentRepo.Remove(attachment);
            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<List<IdeaAttachmentAnnotationDto>?> GetAnnotationsAsync(int attachmentId)
        {
            var attachmentExists = await _attachmentRepo.Query().AnyAsync(a => a.Id == attachmentId);
            if (!attachmentExists)
                return null;

            var annotations = await _annotationRepo.Query()
                .Include(a => a.Author)
                .Where(a => a.IdeaAttachmentId == attachmentId)
                .OrderBy(a => a.CreatedAt)
                .ToListAsync();

            return annotations.Select(MapAnnotation).ToList();
        }

        public async Task<IdeaAttachmentAnnotationDto?> AddAnnotationAsync(int attachmentId, CreateIdeaAttachmentAnnotationDto dto, ClaimsPrincipal caller)
        {
            var attachmentExists = await _attachmentRepo.Query().AnyAsync(a => a.Id == attachmentId);
            if (!attachmentExists)
                return null;

            var annotation = new IdeaAttachmentAnnotation
            {
                IdeaAttachmentId = attachmentId,
                AuthoredBy = GetUserId(caller),
                Body = dto.Body
            };

            _annotationRepo.Add(annotation);
            await _unitOfWork.SaveChangesAsync();

            var saved = await _annotationRepo.Query().Include(a => a.Author).FirstAsync(a => a.Id == annotation.Id);
            return MapAnnotation(saved);
        }

        public async Task<bool> DeleteAnnotationAsync(int annotationId, ClaimsPrincipal caller)
        {
            var annotation = await _annotationRepo.Query().FirstOrDefaultAsync(a => a.Id == annotationId);
            if (annotation == null)
                return false;

            var callerId = GetUserId(caller);
            if (annotation.AuthoredBy != callerId && !caller.HasClaim("permission", "ideas.manage"))
                throw new UnauthorizedAccessException($"Caller lacks access to delete annotation {annotationId}.");

            _annotationRepo.Remove(annotation);
            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        private static IdeaAttachmentDto MapAttachment(IdeaAttachment a) => new()
        {
            Id = a.Id,
            IdeaId = a.IdeaId,
            OriginalFileName = a.OriginalFileName,
            ContentType = a.ContentType,
            FileSize = a.FileSize,
            UploadedBy = a.UploadedBy,
            UploaderName = a.Uploader?.Name ?? "Unknown",
            UploadedAt = a.UploadedAt,
            AnnotationCount = a.Annotations.Count
        };

        private static IdeaAttachmentAnnotationDto MapAnnotation(IdeaAttachmentAnnotation a) => new()
        {
            Id = a.Id,
            AuthoredBy = a.AuthoredBy,
            AuthorName = a.Author?.Name ?? "Unknown",
            Body = a.Body,
            CreatedAt = a.CreatedAt
        };

        private async Task<Idea?> LoadAsync(int id)
        {
            return await _ideaRepo.Query()
                .Include(i => i.Submitter)
                .Include(i => i.ConvertedProject)
                .Include(i => i.Comments)
                .FirstOrDefaultAsync(i => i.Id == id);
        }

        private static IdeaDto MapToDto(Idea i) => new()
        {
            Id = i.Id,
            Title = i.Title,
            Description = i.Description,
            SubmittedBy = i.SubmittedBy,
            SubmitterName = i.Submitter?.Name ?? "Unknown",
            CreatedAt = i.CreatedAt,
            Status = i.Status,
            ConvertedProjectId = i.ConvertedProjectId,
            ConvertedProjectName = i.ConvertedProject?.Name,
            CommentCount = i.Comments.Count(c => !c.IsDeleted)
        };

        private static IdeaCommentDto MapComment(IdeaComment c) => new()
        {
            Id = c.Id,
            AuthoredBy = c.AuthoredBy,
            AuthorName = c.Author?.Name ?? "Unknown",
            Body = c.Body,
            CreatedAt = c.CreatedAt
        };

        private static int GetUserId(ClaimsPrincipal caller)
        {
            var claim = caller.FindFirst(ClaimTypes.NameIdentifier)
                ?? throw new InvalidOperationException("Caller has no NameIdentifier claim.");
            return int.Parse(claim.Value);
        }
    }
}
