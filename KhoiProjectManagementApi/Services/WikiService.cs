using System.Security.Claims;
using KhoiProjectManagement.Models;
using KhoiProjectManagement.Models.DTOs;
using KhoiProjectManagementApi.Authorization;
using KhoiProjectManagementApi.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace KhoiProjectManagementApi.Services
{
    public class WikiService : IWikiService
    {
        private readonly ProjectManagementContext _context;
        private readonly IAuthorizationService _authorizationService;
        private readonly ISpacePermissionResolver _spacePermissionResolver;
        private readonly INotificationService _notificationService;
        private readonly IEmailService _emailService;

        public WikiService(
            ProjectManagementContext context,
            IAuthorizationService authorizationService,
            ISpacePermissionResolver spacePermissionResolver,
            INotificationService notificationService,
            IEmailService emailService)
        {
            _context = context;
            _authorizationService = authorizationService;
            _spacePermissionResolver = spacePermissionResolver;
            _notificationService = notificationService;
            _emailService = emailService;
        }

        public async Task<List<WikiPageSummaryDto>> GetPagesAsync(int spaceId, int? parentPageId, ClaimsPrincipal caller)
        {
            await RequireSpaceAccessAsync(spaceId, caller, PermissionLevel.Read);

            var pages = await _context.WikiPages
                .Where(w => w.SpaceId == spaceId && w.ParentPageId == parentPageId && w.IsActive)
                .OrderBy(w => w.Title)
                .ToListAsync();

            var result = new List<WikiPageSummaryDto>();
            foreach (var page in pages)
            {
                var latestVersion = await GetLatestVersionAsync(page.Id);
                result.Add(new WikiPageSummaryDto
                {
                    Id = page.Id,
                    Title = page.Title,
                    SpaceId = page.SpaceId,
                    ParentPageId = page.ParentPageId,
                    CreatedAt = page.CreatedAt,
                    UpdatedAt = latestVersion?.EditedAt
                });
            }
            return result;
        }

        public async Task<WikiPageDetailDto?> GetPageByIdAsync(int id, ClaimsPrincipal caller)
        {
            var page = await _context.WikiPages
                .Include(w => w.Creator)
                .Include(w => w.Updater)
                .FirstOrDefaultAsync(w => w.Id == id && w.IsActive);
            if (page == null)
                return null;

            await AuthorizePageAsync(page, caller, PermissionLevel.Read);

            var latestVersion = await GetLatestVersionAsync(page.Id);

            return new WikiPageDetailDto
            {
                Id = page.Id,
                Title = page.Title,
                SpaceId = page.SpaceId,
                ParentPageId = page.ParentPageId,
                ContentMarkdown = latestVersion?.ContentMarkdown ?? string.Empty,
                CurrentVersionNumber = latestVersion?.VersionNumber ?? 0,
                CreatorName = page.Creator?.Name ?? "Unknown",
                CreatedAt = page.CreatedAt,
                LastEditedByName = page.Updater?.Name,
                LastEditedAt = page.UpdatedAt
            };
        }

        public async Task<WikiPageDetailDto> CreatePageAsync(CreateWikiPageDto dto, ClaimsPrincipal caller)
        {
            await RequireSpaceAccessAsync(dto.SpaceId, caller, PermissionLevel.Write);

            var userId = GetUserId(caller);
            var page = new WikiPage
            {
                Title = dto.Title,
                SpaceId = dto.SpaceId,
                ParentPageId = dto.ParentPageId,
                CreatedBy = userId
            };
            _context.WikiPages.Add(page);
            await _context.SaveChangesAsync();

            var version = new WikiPageVersion
            {
                WikiPageId = page.Id,
                VersionNumber = 1,
                ContentMarkdown = dto.ContentMarkdown,
                EditedBy = userId
            };
            _context.WikiPageVersions.Add(version);
            await _context.SaveChangesAsync();

            var creator = await _context.Users.FindAsync(userId);
            return new WikiPageDetailDto
            {
                Id = page.Id,
                Title = page.Title,
                SpaceId = page.SpaceId,
                ParentPageId = page.ParentPageId,
                ContentMarkdown = version.ContentMarkdown,
                CurrentVersionNumber = 1,
                CreatorName = creator?.Name ?? "Unknown",
                CreatedAt = page.CreatedAt
            };
        }

        public async Task<bool> UpdatePageAsync(int id, UpdateWikiPageDto dto, ClaimsPrincipal caller)
        {
            var page = await _context.WikiPages.FirstOrDefaultAsync(w => w.Id == id && w.IsActive);
            if (page == null)
                return false;

            await AuthorizePageAsync(page, caller, PermissionLevel.Write);

            var userId = GetUserId(caller);
            var latestVersion = await GetLatestVersionAsync(page.Id);

            page.Title = dto.Title;
            page.UpdatedBy = userId;
            page.UpdatedAt = DateTime.UtcNow;

            // Only content changes are versioned (per the "no Title versioning" design decision) - a
            // pure rename with unchanged content updates the page's Title in place without an extra
            // history row.
            if (latestVersion == null || latestVersion.ContentMarkdown != dto.ContentMarkdown)
            {
                _context.WikiPageVersions.Add(new WikiPageVersion
                {
                    WikiPageId = page.Id,
                    VersionNumber = (latestVersion?.VersionNumber ?? 0) + 1,
                    ContentMarkdown = dto.ContentMarkdown,
                    EditSummary = dto.EditSummary,
                    EditedBy = userId
                });
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeletePageAsync(int id, ClaimsPrincipal caller)
        {
            var page = await _context.WikiPages.FirstOrDefaultAsync(w => w.Id == id && w.IsActive);
            if (page == null)
                return false;

            await AuthorizePageAsync(page, caller, PermissionLevel.Manage);

            page.IsActive = false;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<WikiPageVersionSummaryDto>?> GetVersionsAsync(int id, ClaimsPrincipal caller)
        {
            var page = await _context.WikiPages.FirstOrDefaultAsync(w => w.Id == id);
            if (page == null)
                return null;

            await AuthorizePageAsync(page, caller, PermissionLevel.Read);

            var versions = await _context.WikiPageVersions
                .Include(v => v.Editor)
                .Where(v => v.WikiPageId == id)
                .OrderByDescending(v => v.VersionNumber)
                .ToListAsync();

            return versions.Select(v => new WikiPageVersionSummaryDto
            {
                VersionNumber = v.VersionNumber,
                EditedByName = v.Editor?.Name ?? "Unknown",
                EditedAt = v.EditedAt,
                EditSummary = v.EditSummary
            }).ToList();
        }

        public async Task<WikiPageVersionDetailDto?> GetVersionAsync(int id, int versionNumber, ClaimsPrincipal caller)
        {
            var page = await _context.WikiPages.FirstOrDefaultAsync(w => w.Id == id);
            if (page == null)
                return null;

            await AuthorizePageAsync(page, caller, PermissionLevel.Read);

            var version = await _context.WikiPageVersions
                .Include(v => v.Editor)
                .FirstOrDefaultAsync(v => v.WikiPageId == id && v.VersionNumber == versionNumber);
            if (version == null)
                return null;

            return new WikiPageVersionDetailDto
            {
                VersionNumber = version.VersionNumber,
                ContentMarkdown = version.ContentMarkdown,
                EditedByName = version.Editor?.Name ?? "Unknown",
                EditedAt = version.EditedAt,
                EditSummary = version.EditSummary
            };
        }

        public async Task<List<WikiCommentDto>?> GetCommentsAsync(int id, ClaimsPrincipal caller)
        {
            var page = await _context.WikiPages.FirstOrDefaultAsync(w => w.Id == id);
            if (page == null)
                return null;

            await AuthorizePageAsync(page, caller, PermissionLevel.Read);

            var comments = await _context.WikiPageComments
                .Include(c => c.Author)
                .Where(c => c.WikiPageId == id)
                .OrderBy(c => c.CreatedAt)
                .ToListAsync();

            return comments.Select(c => new WikiCommentDto
            {
                Id = c.Id,
                ParentCommentId = c.ParentCommentId,
                AuthorName = c.IsDeleted ? "[deleted]" : (c.Author?.Name ?? "Unknown"),
                AuthorId = c.AuthoredBy,
                Body = c.IsDeleted ? "[deleted]" : c.Body,
                CreatedAt = c.CreatedAt
            }).ToList();
        }

        public async Task<WikiCommentDto> AddCommentAsync(int id, CreateWikiCommentDto dto, ClaimsPrincipal caller)
        {
            var page = await _context.WikiPages.FirstOrDefaultAsync(w => w.Id == id && w.IsActive)
                ?? throw new InvalidOperationException($"Wiki page {id} not found.");

            await AuthorizePageAsync(page, caller, PermissionLevel.Write);

            var userId = GetUserId(caller);
            var comment = new WikiPageComment
            {
                WikiPageId = id,
                AuthoredBy = userId,
                Body = dto.Body,
                ParentCommentId = dto.ParentCommentId
            };
            _context.WikiPageComments.Add(comment);
            await _context.SaveChangesAsync();

            var author = await _context.Users.FindAsync(userId);
            await NotifyMentionedUsersAsync(page, comment, author?.Name ?? "Unknown", userId);

            return new WikiCommentDto
            {
                Id = comment.Id,
                ParentCommentId = comment.ParentCommentId,
                AuthorName = author?.Name ?? "Unknown",
                AuthorId = userId,
                Body = comment.Body,
                CreatedAt = comment.CreatedAt
            };
        }

        // A mention must never leak the existence or content of a private wiki page - only notify/email
        // a mentioned user who currently holds at least Read on the page's Space, resolved the same way
        // every other access check in this module is (ISpacePermissionResolver), not assumed from the
        // fact that their name appeared in the text.
        private async Task NotifyMentionedUsersAsync(WikiPage page, WikiPageComment comment, string authorName, int authorId)
        {
            var activeUsers = await _context.Users
                .Where(u => u.IsActive)
                .Select(u => new { u.Id, u.Name, u.Email })
                .ToListAsync();

            var mentionedIds = MentionParser.FindMentionedUserIds(
                comment.Body, activeUsers.Select(u => (u.Id, u.Name)), authorId);

            foreach (var mentionedId in mentionedIds)
            {
                var roleIds = await _context.UserRoles
                    .Where(ur => ur.UserId == mentionedId)
                    .Select(ur => ur.RoleId)
                    .ToListAsync();

                var level = await _spacePermissionResolver.ResolveEffectiveLevelAsync(page.SpaceId, mentionedId, roleIds);
                if (level == null)
                    continue; // No Read access to this Space - don't notify or email.

                await _notificationService.CreateNotificationAsync(
                    mentionedId,
                    "mention",
                    $"{authorName} mentioned you in a comment on wiki page '{page.Title}'",
                    wikiPageId: page.Id
                );

                if (await _notificationService.IsEmailEnabledAsync(mentionedId, NotificationTypes.Mention))
                {
                    var user = activeUsers.First(u => u.Id == mentionedId);
                    try
                    {
                        await _emailService.SendMentionEmailAsync(user.Email, authorName, "wiki page", page.Title, comment.Body);
                    }
                    catch
                    {
                        // The comment itself already saved successfully - a failed SMTP send (bad
                        // credentials, host down, etc.) must never fail the comment request. EmailService
                        // already records the failure to EmailLog before re-throwing; swallow it here.
                    }
                }
            }
        }

        public async Task<bool> DeleteCommentAsync(int commentId, ClaimsPrincipal caller)
        {
            var comment = await _context.WikiPageComments
                .Include(c => c.WikiPage)
                .FirstOrDefaultAsync(c => c.Id == commentId && !c.IsDeleted);
            if (comment == null)
                return false;

            var userId = GetUserId(caller);
            if (comment.AuthoredBy != userId)
            {
                // Not the author - must hold Manage on the page's Space instead.
                await AuthorizePageAsync(comment.WikiPage, caller, PermissionLevel.Manage);
            }

            comment.IsDeleted = true;
            await _context.SaveChangesAsync();
            return true;
        }

        private async Task<WikiPageVersion?> GetLatestVersionAsync(int wikiPageId)
        {
            return await _context.WikiPageVersions
                .Where(v => v.WikiPageId == wikiPageId)
                .OrderByDescending(v => v.VersionNumber)
                .FirstOrDefaultAsync();
        }

        private async Task AuthorizePageAsync(WikiPage page, ClaimsPrincipal caller, PermissionLevel level)
        {
            var result = await _authorizationService.AuthorizeAsync(caller, page, new SpacePermissionRequirement(level));
            if (!result.Succeeded)
            {
                throw new UnauthorizedAccessException($"Caller lacks {level} access to wiki page {page.Id}.");
            }
        }

        private async Task RequireSpaceAccessAsync(int spaceId, ClaimsPrincipal caller, PermissionLevel level)
        {
            var result = await _authorizationService.AuthorizeAsync(caller, new SpaceReference(spaceId), new SpacePermissionRequirement(level));
            if (!result.Succeeded)
            {
                throw new UnauthorizedAccessException($"Caller lacks {level} access to space {spaceId}.");
            }
        }

        private static int GetUserId(ClaimsPrincipal caller)
        {
            var claim = caller.FindFirst(ClaimTypes.NameIdentifier)
                ?? throw new InvalidOperationException("Caller has no NameIdentifier claim.");
            return int.Parse(claim.Value);
        }
    }
}
