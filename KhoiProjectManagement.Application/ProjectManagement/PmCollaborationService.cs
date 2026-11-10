using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace KhoiProjectManagement.Application
{
    public interface IPmCollaborationService
    {
        Task<List<WorkItemCommentDto>> GetCommentsAsync(PmCaller caller, int itemId);
        Task<WorkItemCommentDto> AddCommentAsync(PmCaller caller, int itemId, SaveCommentDto dto);
        Task<WorkItemCommentDto> EditCommentAsync(PmCaller caller, int commentId, SaveCommentDto dto);
        Task DeleteCommentAsync(PmCaller caller, int commentId);

        Task<List<ChecklistItemDto>> GetChecklistAsync(PmCaller caller, int itemId);
        Task<ChecklistItemDto> AddChecklistItemAsync(PmCaller caller, int itemId, SaveChecklistItemDto dto);
        Task<ChecklistItemDto> UpdateChecklistItemAsync(PmCaller caller, int checklistItemId, SaveChecklistItemDto dto);
        Task DeleteChecklistItemAsync(PmCaller caller, int checklistItemId);

        Task<List<WorkItemLinkDto>> GetLinksAsync(PmCaller caller, int itemId);
        Task<WorkItemLinkDto> AddLinkAsync(PmCaller caller, int itemId, CreateWorkItemLinkDto dto);
        Task DeleteLinkAsync(PmCaller caller, int linkId);

        Task<List<WorkItemAttachmentDto>> GetAttachmentsAsync(PmCaller caller, int itemId);
        Task<WorkItemAttachmentDto> AddAttachmentAsync(PmCaller caller, int itemId, AttachmentUploadDto file);
        Task<AttachmentDownload> DownloadAttachmentAsync(PmCaller caller, int attachmentId);
        Task DeleteAttachmentAsync(PmCaller caller, int attachmentId);
    }

    // Phase 4 of the PM module: discussion, checklists, dependencies and files on a work item. Everything
    // hangs off the existing ProjectTask row (and the existing Attachment/Notification tables), so the
    // classic Tasks tab and notification centre keep working. Authorization is delegated to
    // IProjectAccessService: View to read, Collaborate to comment/attach, EditItem for checklists/links.
    public class PmCollaborationService : IPmCollaborationService
    {
        private const long DefaultMaxFileSize = 10 * 1024 * 1024;
        // Attachments written by this module live under this key prefix. Rows without it were uploaded
        // through the classic Tasks tab (local disk, bare filename) and are not this module's to list,
        // serve or delete.
        private const string PmKeyPrefix = "pm/";
        // Keeps "pm/{projectId}/{guid}_{name}" inside Attachment.FilePath's 255-character column.
        private const int MaxFileNameLength = 150;

        private readonly IRepository<ProjectTask> _tasks;
        private readonly IRepository<WorkItemComment> _comments;
        private readonly IRepository<ChecklistItem> _checklist;
        private readonly IRepository<WorkItemLink> _links;
        private readonly IRepository<Attachment> _attachments;
        private readonly IRepository<ProjectUser> _members;
        private readonly IRepository<User> _users;
        private readonly IProjectAccessService _access;
        private readonly INotificationService _notifications;
        private readonly IEmailService _email;
        private readonly IActivityLogService _activity;
        private readonly IFileStorage _storage;
        private readonly IConfiguration _config;
        private readonly IUnitOfWork _uow;
        private readonly ILogger<PmCollaborationService> _logger;

        public PmCollaborationService(
            IRepository<ProjectTask> tasks, IRepository<WorkItemComment> comments, IRepository<ChecklistItem> checklist,
            IRepository<WorkItemLink> links, IRepository<Attachment> attachments, IRepository<ProjectUser> members, IRepository<User> users,
            IProjectAccessService access, INotificationService notifications, IEmailService email,
            IActivityLogService activity, IFileStorage storage, IConfiguration config, IUnitOfWork uow,
            ILogger<PmCollaborationService> logger)
        {
            _tasks = tasks; _comments = comments; _checklist = checklist; _links = links; _attachments = attachments;
            _members = members; _users = users; _access = access; _notifications = notifications; _email = email;
            _activity = activity; _storage = storage; _config = config; _uow = uow; _logger = logger;
        }

        // Only attachments this module wrote (see PmKeyPrefix). A static builder so its SQL translation is testable.
        public static IQueryable<Attachment> PmAttachmentsQuery(IQueryable<Attachment> attachments) =>
            attachments.Where(a => a.TaskId != null && a.FilePath.StartsWith(PmKeyPrefix));

        private sealed record ItemRef(int Id, int ProjectId, string Title, int? AssignedToId);

        private async Task<ItemRef> ItemAsync(int itemId) =>
            await _tasks.Query().AsNoTracking().Where(t => t.Id == itemId)
                .Select(t => new ItemRef(t.Id, t.ProjectId, t.Title, t.AssignedToId)).FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException();

        private async Task<bool> CanAsync(PmCaller caller, int projectId, PmAction action)
        {
            var role = await _access.GetRoleAsync(caller, projectId);
            return role == ProjectAccessService.SuperAdminRole || ProjectRoles.Allows(role, action);
        }

        private async Task<string> UserNameAsync(int userId) =>
            await _users.Query().AsNoTracking().Where(u => u.Id == userId).Select(u => u.Name).FirstOrDefaultAsync() ?? "Someone";

        // ---------------------------------------------------------------- comments

        public async Task<List<WorkItemCommentDto>> GetCommentsAsync(PmCaller caller, int itemId)
        {
            var item = await ItemAsync(itemId);
            await _access.RequireAsync(caller, item.ProjectId, PmAction.View);
            var canModerate = await CanAsync(caller, item.ProjectId, PmAction.DeleteItem);

            var rows = await _comments.Query().AsNoTracking()
                .Where(c => c.TaskId == itemId && !c.IsDeleted)
                .OrderBy(c => c.CreatedAt).ThenBy(c => c.Id)
                .Select(c => new { c.Id, c.TaskId, c.AuthoredBy, Name = c.Author.Name, c.Body, c.CreatedAt, c.EditedAt })
                .ToListAsync();

            return rows.Select(c => new WorkItemCommentDto
            {
                Id = c.Id, TaskId = c.TaskId, AuthorId = c.AuthoredBy, AuthorName = c.Name, Body = c.Body,
                CreatedAt = c.CreatedAt, EditedAt = c.EditedAt,
                CanEdit = c.AuthoredBy == caller.UserId,
                CanDelete = c.AuthoredBy == caller.UserId || canModerate,
            }).ToList();
        }

        public async Task<WorkItemCommentDto> AddCommentAsync(PmCaller caller, int itemId, SaveCommentDto dto)
        {
            var item = await ItemAsync(itemId);
            await _access.RequireAsync(caller, item.ProjectId, PmAction.Collaborate);

            var comment = new WorkItemComment { TaskId = itemId, AuthoredBy = caller.UserId, Body = dto.Body.Trim() };
            _comments.Add(comment);
            await _uow.SaveChangesAsync();

            await NotifyAsync(caller, item, comment.Body, isNewComment: true);
            await _activity.LogAsync("WorkItem", item.Id, item.Title, caller.UserId, "commented");

            return new WorkItemCommentDto
            {
                Id = comment.Id, TaskId = itemId, AuthorId = caller.UserId, AuthorName = await UserNameAsync(caller.UserId),
                Body = comment.Body, CreatedAt = comment.CreatedAt, CanEdit = true, CanDelete = true,
            };
        }

        public async Task<WorkItemCommentDto> EditCommentAsync(PmCaller caller, int commentId, SaveCommentDto dto)
        {
            var comment = await _comments.Query().FirstOrDefaultAsync(c => c.Id == commentId && !c.IsDeleted)
                ?? throw new KeyNotFoundException();
            var item = await ItemAsync(comment.TaskId);
            await _access.RequireAsync(caller, item.ProjectId, PmAction.Collaborate);
            if (comment.AuthoredBy != caller.UserId)
                throw new ForbiddenException("You can only edit your own comments.");

            var previous = comment.Body;
            comment.Body = dto.Body.Trim();
            comment.EditedAt = DateTime.UtcNow;
            await _uow.SaveChangesAsync();

            // Only people newly @mentioned by the edit are notified; those already mentioned were told once.
            await NotifyAsync(caller, item, comment.Body, isNewComment: false, previousBody: previous);

            return new WorkItemCommentDto
            {
                Id = comment.Id, TaskId = comment.TaskId, AuthorId = comment.AuthoredBy, AuthorName = await UserNameAsync(comment.AuthoredBy),
                Body = comment.Body, CreatedAt = comment.CreatedAt, EditedAt = comment.EditedAt, CanEdit = true, CanDelete = true,
            };
        }

        public async Task DeleteCommentAsync(PmCaller caller, int commentId)
        {
            var comment = await _comments.Query().FirstOrDefaultAsync(c => c.Id == commentId && !c.IsDeleted)
                ?? throw new KeyNotFoundException();
            var item = await ItemAsync(comment.TaskId);
            await _access.RequireAsync(caller, item.ProjectId, PmAction.View);
            if (comment.AuthoredBy != caller.UserId)
                await _access.RequireAsync(caller, item.ProjectId, PmAction.DeleteItem);

            comment.IsDeleted = true;
            await _uow.SaveChangesAsync();
        }

        // Notifies the assignee of a new comment and anyone @mentioned (project members only - a mention
        // must never reveal an item to someone who cannot open it). Email is best-effort: the comment is
        // already saved, so a failed send is logged, never surfaced.
        private async Task NotifyAsync(PmCaller caller, ItemRef item, string body, bool isNewComment, string? previousBody = null)
        {
            var people = await _members.Query().AsNoTracking()
                .Where(m => m.ProjectId == item.ProjectId && m.User.IsActive)
                .Select(m => new { m.UserId, m.User.Name, m.User.Email })
                .ToListAsync();
            var candidates = people.Select(p => (p.UserId, p.Name)).ToList();
            var authorName = await UserNameAsync(caller.UserId);

            var mentioned = MentionParser.FindMentionedUserIds(body, candidates, caller.UserId);
            if (previousBody != null)
            {
                var before = MentionParser.FindMentionedUserIds(previousBody, candidates, caller.UserId);
                mentioned = mentioned.Except(before).ToList();
            }

            foreach (var userId in mentioned)
            {
                await _notifications.CreateNotificationAsync(userId, NotificationTypes.Mention,
                    $"{authorName} mentioned you on '{item.Title}'", taskId: item.Id, projectId: item.ProjectId);
                if (!await _notifications.IsEmailEnabledAsync(userId, NotificationTypes.Mention)) continue;
                try
                {
                    await _email.SendMentionEmailAsync(people.First(p => p.UserId == userId).Email, authorName, "work item", item.Title, body);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Mention email for work item {ItemId} failed", item.Id);
                }
            }

            if (isNewComment && item.AssignedToId is int assignee && assignee != caller.UserId && !mentioned.Contains(assignee))
            {
                await _notifications.CreateNotificationAsync(assignee, NotificationTypes.Comment,
                    $"{authorName} commented on '{item.Title}'", taskId: item.Id, projectId: item.ProjectId);
            }
        }

        // ---------------------------------------------------------------- checklist

        public async Task<List<ChecklistItemDto>> GetChecklistAsync(PmCaller caller, int itemId)
        {
            var item = await ItemAsync(itemId);
            await _access.RequireAsync(caller, item.ProjectId, PmAction.View);
            return await _checklist.Query().AsNoTracking().Where(c => c.TaskId == itemId)
                .OrderBy(c => c.Position).ThenBy(c => c.Id)
                .Select(c => new ChecklistItemDto { Id = c.Id, Text = c.Text, IsDone = c.IsDone, Position = c.Position })
                .ToListAsync();
        }

        public async Task<ChecklistItemDto> AddChecklistItemAsync(PmCaller caller, int itemId, SaveChecklistItemDto dto)
        {
            var item = await ItemAsync(itemId);
            await _access.RequireAsync(caller, item.ProjectId, PmAction.EditItem);
            var next = (await _checklist.Query().Where(c => c.TaskId == itemId).Select(c => (int?)c.Position).MaxAsync() ?? 0) + 1;
            var entry = new ChecklistItem { TaskId = itemId, Text = dto.Text.Trim(), Position = next };
            _checklist.Add(entry);
            await _uow.SaveChangesAsync();
            return Map(entry);
        }

        public async Task<ChecklistItemDto> UpdateChecklistItemAsync(PmCaller caller, int checklistItemId, SaveChecklistItemDto dto)
        {
            var entry = await _checklist.Query().FirstOrDefaultAsync(c => c.Id == checklistItemId) ?? throw new KeyNotFoundException();
            var item = await ItemAsync(entry.TaskId);
            await _access.RequireAsync(caller, item.ProjectId, PmAction.EditItem);

            entry.Text = dto.Text.Trim();
            if (dto.IsDone != entry.IsDone)
            {
                entry.IsDone = dto.IsDone;
                entry.CompletedAt = dto.IsDone ? DateTime.UtcNow : null;
                entry.CompletedBy = dto.IsDone ? caller.UserId : null;
            }
            await _uow.SaveChangesAsync();
            return Map(entry);
        }

        public async Task DeleteChecklistItemAsync(PmCaller caller, int checklistItemId)
        {
            var entry = await _checklist.Query().FirstOrDefaultAsync(c => c.Id == checklistItemId) ?? throw new KeyNotFoundException();
            var item = await ItemAsync(entry.TaskId);
            await _access.RequireAsync(caller, item.ProjectId, PmAction.EditItem);
            _checklist.Remove(entry);
            await _uow.SaveChangesAsync();
        }

        private static ChecklistItemDto Map(ChecklistItem c) => new() { Id = c.Id, Text = c.Text, IsDone = c.IsDone, Position = c.Position };

        // ---------------------------------------------------------------- links / dependencies

        public async Task<List<WorkItemLinkDto>> GetLinksAsync(PmCaller caller, int itemId)
        {
            var item = await ItemAsync(itemId);
            await _access.RequireAsync(caller, item.ProjectId, PmAction.View);

            var links = await _links.Query().AsNoTracking()
                .Where(l => l.SourceId == itemId || l.TargetId == itemId)
                .OrderBy(l => l.Id)
                .Select(l => new { l.Id, l.Type, l.SourceId, l.TargetId })
                .ToListAsync();
            if (links.Count == 0) return new List<WorkItemLinkDto>();

            var otherIds = links.Select(l => l.SourceId == itemId ? l.TargetId : l.SourceId).Distinct().ToList();
            var others = (await _tasks.Query().AsNoTracking().Where(t => otherIds.Contains(t.Id))
                .Select(t => new { t.Id, t.Title, t.Type, t.Status }).ToListAsync()).ToDictionary(t => t.Id);

            return links.Where(l => others.ContainsKey(l.SourceId == itemId ? l.TargetId : l.SourceId)).Select(l =>
            {
                var outgoing = l.SourceId == itemId;
                var other = others[outgoing ? l.TargetId : l.SourceId];
                return new WorkItemLinkDto
                {
                    Id = l.Id, Type = l.Type, IsOutgoing = outgoing, Relation = Relation(l.Type, outgoing),
                    OtherId = other.Id, OtherTitle = other.Title, OtherType = other.Type, OtherStatus = other.Status,
                };
            }).ToList();
        }

        public static string Relation(string type, bool outgoing) => type switch
        {
            WorkItemLinkTypes.Blocks => outgoing ? "Blocks" : "Is blocked by",
            WorkItemLinkTypes.Duplicates => outgoing ? "Duplicates" : "Is duplicated by",
            _ => "Relates to",
        };

        public async Task<WorkItemLinkDto> AddLinkAsync(PmCaller caller, int itemId, CreateWorkItemLinkDto dto)
        {
            var item = await ItemAsync(itemId);
            await _access.RequireAsync(caller, item.ProjectId, PmAction.EditItem);

            if (dto.TargetId == itemId) throw new InvalidOperationException("An item cannot be linked to itself.");
            if (!await _tasks.Query().AnyAsync(t => t.Id == dto.TargetId && t.ProjectId == item.ProjectId))
                throw new InvalidOperationException("The linked item does not exist in this project.");

            if (await _links.Query().AnyAsync(l => (l.SourceId == itemId && l.TargetId == dto.TargetId) || (l.SourceId == dto.TargetId && l.TargetId == itemId)))
                throw new InvalidOperationException("These items are already linked.");

            if (dto.Type == WorkItemLinkTypes.Blocks && await WouldCreateBlockCycleAsync(item.ProjectId, itemId, dto.TargetId))
                throw new InvalidOperationException("That would create a circular dependency.");

            var link = new WorkItemLink
            {
                SourceId = itemId, TargetId = dto.TargetId, Type = dto.Type, CreatedBy = caller.UserId,
                PairKey = WorkItemLink.KeyFor(itemId, dto.TargetId),
            };
            _links.Add(link);
            try
            {
                await _uow.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // The unique pair index caught a concurrent request linking the same two items.
                throw new InvalidOperationException("These items are already linked.");
            }

            // Two simultaneous "blocks" requests can each pass the check above and together close a loop.
            // Re-check once the edge is stored; the loser (possibly both) backs out.
            if (dto.Type == WorkItemLinkTypes.Blocks && await WouldCreateBlockCycleAsync(item.ProjectId, itemId, dto.TargetId))
            {
                _links.Remove(link);
                await _uow.SaveChangesAsync();
                throw new InvalidOperationException("That would create a circular dependency.");
            }
            await _activity.LogAsync("WorkItem", item.Id, item.Title, caller.UserId, "linked", $"{dto.Type} #{dto.TargetId}");

            return (await GetLinksAsync(caller, itemId)).First(l => l.Id == link.Id);
        }

        // A new "source blocks target" edge closes a cycle if target already (transitively) blocks source.
        private async Task<bool> WouldCreateBlockCycleAsync(int projectId, int sourceId, int targetId)
        {
            var edges = await _links.Query().AsNoTracking()
                .Where(l => l.Type == WorkItemLinkTypes.Blocks && l.Source.ProjectId == projectId)
                .Select(l => new { l.SourceId, l.TargetId }).ToListAsync();
            var next = edges.ToLookup(e => e.SourceId, e => e.TargetId);

            var seen = new HashSet<int>();
            var stack = new Stack<int>();
            stack.Push(targetId);
            while (stack.Count > 0)
            {
                var current = stack.Pop();
                if (current == sourceId) return true;
                if (!seen.Add(current)) continue;
                foreach (var n in next[current]) stack.Push(n);
            }
            return false;
        }

        public async Task DeleteLinkAsync(PmCaller caller, int linkId)
        {
            var link = await _links.Query().FirstOrDefaultAsync(l => l.Id == linkId) ?? throw new KeyNotFoundException();
            var item = await ItemAsync(link.SourceId);
            await _access.RequireAsync(caller, item.ProjectId, PmAction.EditItem);
            _links.Remove(link);
            await _uow.SaveChangesAsync();
        }

        // ---------------------------------------------------------------- attachments

        public async Task<List<WorkItemAttachmentDto>> GetAttachmentsAsync(PmCaller caller, int itemId)
        {
            var item = await ItemAsync(itemId);
            await _access.RequireAsync(caller, item.ProjectId, PmAction.View);
            var canModerate = await CanAsync(caller, item.ProjectId, PmAction.DeleteItem);

            var rows = await PmAttachmentsQuery(_attachments.Query()).AsNoTracking().Where(a => a.TaskId == itemId)
                .OrderBy(a => a.UploadedAt)
                .Select(a => new { a.Id, a.FileName, a.FileSize, a.ContentType, a.UploadedBy, Name = a.UploadedByUser.Name, a.UploadedAt })
                .ToListAsync();

            return rows.Select(a => new WorkItemAttachmentDto
            {
                Id = a.Id, FileName = a.FileName, FileSize = a.FileSize, ContentType = a.ContentType, UploadedBy = a.Name,
                UploadedAt = a.UploadedAt, IsImage = IsImage(a.ContentType),
                CanDelete = a.UploadedBy == caller.UserId || canModerate,
            }).ToList();
        }

        public async Task<WorkItemAttachmentDto> AddAttachmentAsync(PmCaller caller, int itemId, AttachmentUploadDto file)
        {
            var item = await ItemAsync(itemId);
            await _access.RequireAsync(caller, item.ProjectId, PmAction.Collaborate);

            var maxSize = long.TryParse(_config["FileUpload:MaxFileSize"], out var configured) ? configured : DefaultMaxFileSize;
            if (file.Length <= 0) throw new InvalidOperationException("The file is empty.");
            if (file.Length > maxSize) throw new InvalidOperationException($"The file is larger than the {maxSize / (1024 * 1024)} MB limit.");

            var allowed = _config.GetSection("FileUpload:AllowedExtensions").Get<string[]>() ?? Array.Empty<string>();
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowed.Contains(extension)) throw new InvalidOperationException("That file type is not allowed.");

            var displayName = UploadFileNaming.SanitizeFileName(file.FileName);
            if (displayName.Length > MaxFileNameLength)
                throw new InvalidOperationException($"The file name is too long (maximum {MaxFileNameLength} characters).");

            var contentType = ResolveContentType(file.ContentType, displayName);
            var key = $"{PmKeyPrefix}{item.ProjectId}/{UploadFileNaming.BuildStoredFileName(file.FileName)}";
            await _storage.SaveAsync(key, file.Content, contentType);

            var attachment = new Attachment
            {
                FileName = displayName, FilePath = key, FileSize = file.Length,
                ContentType = contentType, TaskId = itemId, UploadedBy = caller.UserId,
            };
            _attachments.Add(attachment);
            try
            {
                await _uow.SaveChangesAsync();
            }
            catch
            {
                await _storage.DeleteAsync(key); // don't strand an orphan object when the row fails to save
                throw;
            }
            await _activity.LogAsync("WorkItem", item.Id, item.Title, caller.UserId, "attached", attachment.FileName);

            return new WorkItemAttachmentDto
            {
                Id = attachment.Id, FileName = attachment.FileName, FileSize = attachment.FileSize, ContentType = attachment.ContentType,
                UploadedBy = await UserNameAsync(caller.UserId), UploadedAt = attachment.UploadedAt,
                IsImage = IsImage(attachment.ContentType), CanDelete = true,
            };
        }

        public async Task<AttachmentDownload> DownloadAttachmentAsync(PmCaller caller, int attachmentId)
        {
            var attachment = await PmAttachmentsQuery(_attachments.Query().AsNoTracking()).FirstOrDefaultAsync(a => a.Id == attachmentId)
                ?? throw new KeyNotFoundException();
            var item = await ItemAsync(attachment.TaskId!.Value);
            await _access.RequireAsync(caller, item.ProjectId, PmAction.View);
            var stream = await _storage.OpenReadAsync(attachment.FilePath) ?? throw new KeyNotFoundException();
            return new AttachmentDownload { Content = stream, FileName = attachment.FileName, ContentType = attachment.ContentType };
        }

        public async Task DeleteAttachmentAsync(PmCaller caller, int attachmentId)
        {
            var attachment = await PmAttachmentsQuery(_attachments.Query()).FirstOrDefaultAsync(a => a.Id == attachmentId)
                ?? throw new KeyNotFoundException();
            var item = await ItemAsync(attachment.TaskId!.Value);
            await _access.RequireAsync(caller, item.ProjectId, PmAction.View);
            if (attachment.UploadedBy != caller.UserId)
                await _access.RequireAsync(caller, item.ProjectId, PmAction.DeleteItem);

            _attachments.Remove(attachment);
            await _uow.SaveChangesAsync();
            try
            {
                await _storage.DeleteAsync(attachment.FilePath);
            }
            catch (Exception ex)
            {
                // The row is already gone; a failed object delete must not turn that into an error.
                _logger.LogWarning(ex, "Could not delete stored object {Key} for attachment {AttachmentId}", attachment.FilePath, attachment.Id);
            }
        }

        // The browser-supplied type is untrusted: it is stored and later handed to File(), where a malformed
        // value would throw on every download. Prefer the type implied by the (allow-listed) extension, and
        // only fall back to the client's when it is a well-formed media type that fits the column.
        private static string ResolveContentType(string? clientType, string fileName)
        {
            var fromExtension = ContentTypeResolver.Resolve(null, fileName);
            if (fromExtension != "application/octet-stream") return fromExtension;
            return !string.IsNullOrWhiteSpace(clientType) && clientType.Length <= 100
                && System.Net.Http.Headers.MediaTypeHeaderValue.TryParse(clientType, out _)
                ? clientType
                : "application/octet-stream";
        }

        private static bool IsImage(string contentType) => contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
    }
}
