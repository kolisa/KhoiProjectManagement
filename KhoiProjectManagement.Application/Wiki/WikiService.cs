using System.Security.Claims;
using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using KhoiProjectManagement.Application;
using KhoiProjectManagement.Application.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace KhoiProjectManagement.Application
{
    public class WikiService : IWikiService
    {
        private readonly IRepository<WikiPage> _pageRepo;
        private readonly IRepository<UserRole> _userRoleRepo;
        private readonly IRepository<UserGroup> _userGroupRepo;
        private readonly IRepository<WikiPageVersion> _versionRepo;
        private readonly IRepository<User> _userRepo;
        private readonly IRepository<WikiPageTag> _pageTagRepo;
        private readonly IRepository<Tag> _tagRepo;
        private readonly IRepository<WikiPageComment> _commentRepo;
        private readonly IWikiSearchRepository _wikiSearchRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAuthorizationService _authorizationService;
        private readonly ISpacePermissionResolver _spacePermissionResolver;
        private readonly INotificationService _notificationService;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;

        public WikiService(
            IRepository<WikiPage> pageRepo,
            IRepository<UserRole> userRoleRepo,
            IRepository<UserGroup> userGroupRepo,
            IRepository<WikiPageVersion> versionRepo,
            IRepository<User> userRepo,
            IRepository<WikiPageTag> pageTagRepo,
            IRepository<Tag> tagRepo,
            IRepository<WikiPageComment> commentRepo,
            IWikiSearchRepository wikiSearchRepo,
            IUnitOfWork unitOfWork,
            IAuthorizationService authorizationService,
            ISpacePermissionResolver spacePermissionResolver,
            INotificationService notificationService,
            IEmailService emailService,
            IConfiguration configuration)
        {
            _pageRepo = pageRepo;
            _userRoleRepo = userRoleRepo;
            _userGroupRepo = userGroupRepo;
            _versionRepo = versionRepo;
            _userRepo = userRepo;
            _pageTagRepo = pageTagRepo;
            _tagRepo = tagRepo;
            _commentRepo = commentRepo;
            _wikiSearchRepo = wikiSearchRepo;
            _unitOfWork = unitOfWork;
            _authorizationService = authorizationService;
            _spacePermissionResolver = spacePermissionResolver;
            _notificationService = notificationService;
            _emailService = emailService;
            _configuration = configuration;
        }

        public async Task<PermissionLevel?> GetMyLevelForPageAsync(int pageId, ClaimsPrincipal caller)
        {
            var page = await _pageRepo.Query().FirstOrDefaultAsync(p => p.Id == pageId && p.IsActive);
            if (page == null)
                return null;

            var userId = GetUserId(caller);
            var roleIds = await _userRoleRepo.Query()
                .Where(ur => ur.UserId == userId)
                .Select(ur => ur.RoleId)
                .ToListAsync();
            var groupIds = await _userGroupRepo.Query()
                .Where(ug => ug.UserId == userId)
                .Select(ug => ug.GroupId)
                .ToListAsync();

            return await _spacePermissionResolver.ResolveEffectiveLevelAsync(page.SpaceId, userId, roleIds, groupIds);
        }

        public async Task<List<WikiPageSummaryDto>> GetPagesAsync(int spaceId, int? parentPageId, ClaimsPrincipal caller)
        {
            await RequireSpaceAccessAsync(spaceId, caller, PermissionLevel.Read);

            var pages = await _pageRepo.Query()
                .Include(w => w.WikiPageTags).ThenInclude(t => t.Tag)
                .Where(w => w.SpaceId == spaceId && w.ParentPageId == parentPageId && w.IsActive)
                .OrderBy(w => w.SortOrder)
                .ThenBy(w => w.Title)
                .ToListAsync();

            // One batched query for every page's latest version instead of one round-trip per page.
            var pageIds = pages.Select(p => p.Id).ToList();
            var latestVersions = await _versionRepo.Query()
                .Where(v => pageIds.Contains(v.WikiPageId))
                .GroupBy(v => v.WikiPageId)
                .Select(g => g.OrderByDescending(v => v.VersionNumber).First())
                .ToListAsync();
            var latestByPageId = latestVersions.ToDictionary(v => v.WikiPageId);

            var result = new List<WikiPageSummaryDto>();
            foreach (var page in pages)
            {
                latestByPageId.TryGetValue(page.Id, out var latestVersion);
                result.Add(new WikiPageSummaryDto
                {
                    Id = page.Id,
                    Title = page.Title,
                    SpaceId = page.SpaceId,
                    ParentPageId = page.ParentPageId,
                    SortOrder = page.SortOrder,
                    CreatedAt = page.CreatedAt,
                    UpdatedAt = latestVersion?.EditedAt,
                    Labels = page.WikiPageTags.Select(t => t.Tag.Name).OrderBy(n => n).ToList(),
                    WordCount = (page.CurrentContentMarkdown ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length
                });
            }
            return result;
        }

        public async Task<WikiPageDetailDto?> GetPageByIdAsync(int id, ClaimsPrincipal caller)
        {
            var page = await _pageRepo.Query()
                .Include(w => w.Creator)
                .Include(w => w.Updater)
                .Include(w => w.WikiPageTags).ThenInclude(t => t.Tag)
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
                LastEditedAt = page.UpdatedAt,
                Labels = page.WikiPageTags.Select(t => t.Tag.Name).OrderBy(n => n).ToList()
            };
        }

        public async Task<WikiPageDetailDto> CreatePageAsync(CreateWikiPageDto dto, ClaimsPrincipal caller)
        {
            await RequireSpaceAccessAsync(dto.SpaceId, caller, PermissionLevel.Write);

            var userId = GetUserId(caller);
            var nextSortOrder = await _pageRepo.Query()
                .Where(w => w.SpaceId == dto.SpaceId && w.ParentPageId == dto.ParentPageId && w.IsActive)
                .Select(w => (int?)w.SortOrder)
                .MaxAsync() ?? -1;

            var page = new WikiPage
            {
                Title = dto.Title,
                SpaceId = dto.SpaceId,
                ParentPageId = dto.ParentPageId,
                CreatedBy = userId,
                CurrentContentMarkdown = dto.ContentMarkdown,
                SortOrder = nextSortOrder + 1
            };
            _pageRepo.Add(page);
            await _unitOfWork.SaveChangesAsync();

            var version = new WikiPageVersion
            {
                WikiPageId = page.Id,
                VersionNumber = 1,
                ContentMarkdown = dto.ContentMarkdown,
                EditedBy = userId
            };
            _versionRepo.Add(version);
            await _unitOfWork.SaveChangesAsync();

            var creator = await _userRepo.FindAsync(userId);
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
            var page = await _pageRepo.Query().FirstOrDefaultAsync(w => w.Id == id && w.IsActive);
            if (page == null)
                return false;

            await AuthorizePageAsync(page, caller, PermissionLevel.Write);

            var userId = GetUserId(caller);
            var latestVersion = await GetLatestVersionAsync(page.Id);

            page.Title = dto.Title;
            page.UpdatedBy = userId;
            page.UpdatedAt = DateTime.UtcNow;
            page.CurrentContentMarkdown = dto.ContentMarkdown;

            // Only content changes are versioned (per the "no Title versioning" design decision) - a
            // pure rename with unchanged content updates the page's Title in place without an extra
            // history row. CurrentContentMarkdown (search index source) stays in sync either way.
            if (latestVersion == null || latestVersion.ContentMarkdown != dto.ContentMarkdown)
            {
                _versionRepo.Add(new WikiPageVersion
                {
                    WikiPageId = page.Id,
                    VersionNumber = (latestVersion?.VersionNumber ?? 0) + 1,
                    ContentMarkdown = dto.ContentMarkdown,
                    EditSummary = dto.EditSummary,
                    EditedBy = userId
                });
            }

            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeletePageAsync(int id, ClaimsPrincipal caller)
        {
            var page = await _pageRepo.Query().FirstOrDefaultAsync(w => w.Id == id && w.IsActive);
            if (page == null)
                return false;

            await AuthorizePageAsync(page, caller, PermissionLevel.Manage);

            page.IsActive = false;
            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<bool> MovePageAsync(int id, MoveWikiPageDto dto, ClaimsPrincipal caller)
        {
            var page = await _pageRepo.Query().FirstOrDefaultAsync(w => w.Id == id && w.IsActive);
            if (page == null)
                return false;

            await AuthorizePageAsync(page, caller, PermissionLevel.Write);

            if (dto.NewParentPageId.HasValue)
            {
                if (dto.NewParentPageId.Value == id)
                    throw new InvalidOperationException("A page cannot be its own parent.");

                var newParent = await _pageRepo.Query().FirstOrDefaultAsync(w => w.Id == dto.NewParentPageId.Value && w.IsActive)
                    ?? throw new InvalidOperationException("Target parent page not found.");

                // ParentPageId nests pages within one Space for navigation only (see WikiPage's own
                // comment) - it must still stay within that one Space, never jump the page to another.
                if (newParent.SpaceId != page.SpaceId)
                    throw new InvalidOperationException("Cannot move a page to a parent in a different Space.");

                // Reject creating a cycle: walk up from the proposed new parent and make sure this page
                // isn't one of its own ancestors-to-be.
                var walker = newParent;
                while (walker != null)
                {
                    if (walker.Id == id)
                        throw new InvalidOperationException("Cannot move a page under one of its own descendants.");
                    walker = walker.ParentPageId.HasValue
                        ? await _pageRepo.Query().FirstOrDefaultAsync(w => w.Id == walker.ParentPageId.Value)
                        : null;
                }
            }

            var nextSortOrder = await _pageRepo.Query()
                .Where(w => w.SpaceId == page.SpaceId && w.ParentPageId == dto.NewParentPageId && w.IsActive && w.Id != id)
                .Select(w => (int?)w.SortOrder)
                .MaxAsync() ?? -1;

            page.ParentPageId = dto.NewParentPageId;
            page.SortOrder = nextSortOrder + 1;
            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ReorderPagesAsync(int spaceId, int? parentPageId, ReorderWikiPagesDto dto, ClaimsPrincipal caller)
        {
            await RequireSpaceAccessAsync(spaceId, caller, PermissionLevel.Write);

            var siblings = await _pageRepo.Query()
                .Where(w => w.SpaceId == spaceId && w.ParentPageId == parentPageId && w.IsActive)
                .ToListAsync();

            // Every id in the submitted order must actually belong to this sibling group - otherwise a
            // caller could smuggle in an unrelated page's id and silently re-parent it by omission.
            var siblingIds = siblings.Select(s => s.Id).ToHashSet();
            if (dto.OrderedPageIds.Count != siblings.Count || !dto.OrderedPageIds.All(siblingIds.Contains))
                throw new InvalidOperationException("Submitted order must contain exactly this group's current pages.");

            for (var i = 0; i < dto.OrderedPageIds.Count; i++)
            {
                siblings.First(s => s.Id == dto.OrderedPageIds[i]).SortOrder = i;
            }

            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<bool> SetLabelsAsync(int id, SetWikiPageLabelsDto dto, ClaimsPrincipal caller)
        {
            var page = await _pageRepo.Query()
                .Include(w => w.WikiPageTags)
                .FirstOrDefaultAsync(w => w.Id == id && w.IsActive);
            if (page == null)
                return false;

            await AuthorizePageAsync(page, caller, PermissionLevel.Write);

            _pageTagRepo.RemoveRange(page.WikiPageTags);

            foreach (var rawLabel in dto.Labels.Select(l => l.Trim().ToLower()).Where(l => l.Length > 0).Distinct())
            {
                var tag = await _tagRepo.Query().FirstOrDefaultAsync(t => t.Name == rawLabel);
                if (tag == null)
                {
                    tag = new Tag { Name = rawLabel };
                    _tagRepo.Add(tag);
                    await _unitOfWork.SaveChangesAsync();
                }

                _pageTagRepo.Add(new WikiPageTag { WikiPageId = id, TagId = tag.Id });
            }

            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<List<WikiPageVersionSummaryDto>?> GetVersionsAsync(int id, ClaimsPrincipal caller)
        {
            var page = await _pageRepo.Query().FirstOrDefaultAsync(w => w.Id == id);
            if (page == null)
                return null;

            await AuthorizePageAsync(page, caller, PermissionLevel.Read);

            var versions = await _versionRepo.Query()
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
            var page = await _pageRepo.Query().FirstOrDefaultAsync(w => w.Id == id);
            if (page == null)
                return null;

            await AuthorizePageAsync(page, caller, PermissionLevel.Read);

            var version = await _versionRepo.Query()
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
            var page = await _pageRepo.Query().FirstOrDefaultAsync(w => w.Id == id);
            if (page == null)
                return null;

            await AuthorizePageAsync(page, caller, PermissionLevel.Read);

            var comments = await _commentRepo.Query()
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
                CreatedAt = c.CreatedAt,
                AnchorBlockIndex = c.AnchorBlockIndex,
                AnchorText = c.AnchorText
            }).ToList();
        }

        public async Task<WikiCommentDto> AddCommentAsync(int id, CreateWikiCommentDto dto, ClaimsPrincipal caller)
        {
            var page = await _pageRepo.Query().FirstOrDefaultAsync(w => w.Id == id && w.IsActive)
                ?? throw new InvalidOperationException($"Wiki page {id} not found.");

            await AuthorizePageAsync(page, caller, PermissionLevel.Write);

            var userId = GetUserId(caller);
            var comment = new WikiPageComment
            {
                WikiPageId = id,
                AuthoredBy = userId,
                Body = dto.Body,
                ParentCommentId = dto.ParentCommentId,
                AnchorBlockIndex = dto.AnchorBlockIndex,
                AnchorText = dto.AnchorText
            };
            _commentRepo.Add(comment);
            await _unitOfWork.SaveChangesAsync();

            var author = await _userRepo.FindAsync(userId);
            await NotifyMentionedUsersAsync(page, comment, author?.Name ?? "Unknown", userId);

            return new WikiCommentDto
            {
                Id = comment.Id,
                ParentCommentId = comment.ParentCommentId,
                AuthorName = author?.Name ?? "Unknown",
                AuthorId = userId,
                Body = comment.Body,
                CreatedAt = comment.CreatedAt,
                AnchorBlockIndex = comment.AnchorBlockIndex,
                AnchorText = comment.AnchorText
            };
        }

        // A mention must never leak the existence or content of a private wiki page - only notify/email
        // a mentioned user who currently holds at least Read on the page's Space, resolved the same way
        // every other access check in this module is (ISpacePermissionResolver), not assumed from the
        // fact that their name appeared in the text.
        private async Task NotifyMentionedUsersAsync(WikiPage page, WikiPageComment comment, string authorName, int authorId)
        {
            var activeUsers = await _userRepo.Query()
                .Where(u => u.IsActive)
                .Select(u => new { u.Id, u.Name, u.Email })
                .ToListAsync();

            var mentionedIds = MentionParser.FindMentionedUserIds(
                comment.Body, activeUsers.Select(u => (u.Id, u.Name)), authorId);

            foreach (var mentionedId in mentionedIds)
            {
                var roleIds = await _userRoleRepo.Query()
                    .Where(ur => ur.UserId == mentionedId)
                    .Select(ur => ur.RoleId)
                    .ToListAsync();
                var groupIds = await _userGroupRepo.Query()
                    .Where(ug => ug.UserId == mentionedId)
                    .Select(ug => ug.GroupId)
                    .ToListAsync();

                var level = await _spacePermissionResolver.ResolveEffectiveLevelAsync(page.SpaceId, mentionedId, roleIds, groupIds);
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
            var comment = await _commentRepo.Query()
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
            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<List<WikiSearchResultDto>> SearchPagesAsync(string query, ClaimsPrincipal caller)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new List<WikiSearchResultDto>();

            // Rank/match computed at query time (not a stored generated column - see WikiPage.
            // CurrentContentMarkdown's comment for why) against a bounded candidate set, THEN
            // permission-filtered - cheap since ISpacePermissionResolver caches its snapshot, and
            // correct: a text match alone must never surface a page the caller can't actually Read.
            // Delegated to IWikiSearchRepository since Postgres's ToTsVector/PlainToTsQuery are
            // provider-specific and only resolvable against the concrete Npgsql provider (Infrastructure
            // only) - see that interface for why this is a deliberate exception to IRepository<T>.
            var candidates = await _wikiSearchRepo.FindCandidatesAsync(query, 50);

            var callerId = GetUserId(caller);
            var roleIds = await _userRoleRepo.Query()
                .Where(ur => ur.UserId == callerId)
                .Select(ur => ur.RoleId)
                .ToListAsync();
            var groupIds = await _userGroupRepo.Query()
                .Where(ug => ug.UserId == callerId)
                .Select(ug => ug.GroupId)
                .ToListAsync();

            var results = new List<WikiSearchResultDto>();
            foreach (var page in candidates)
            {
                var level = await _spacePermissionResolver.ResolveEffectiveLevelAsync(page.SpaceId, callerId, roleIds, groupIds);
                if (level == null)
                    continue;

                results.Add(new WikiSearchResultDto
                {
                    Id = page.Id,
                    Title = page.Title,
                    SpaceId = page.SpaceId,
                    SpaceName = page.Space?.Name ?? "Unknown",
                    Snippet = BuildSnippet(page.CurrentContentMarkdown, query)
                });

                if (results.Count >= 20)
                    break;
            }

            return results;
        }

        private static string BuildSnippet(string? content, string query)
        {
            if (string.IsNullOrEmpty(content))
                return string.Empty;

            var idx = content.IndexOf(query, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
                return content.Length > 160 ? content[..160] + "..." : content;

            var start = Math.Max(0, idx - 60);
            var length = Math.Min(160, content.Length - start);
            var snippet = content.Substring(start, length);
            return (start > 0 ? "..." : "") + snippet + (start + length < content.Length ? "..." : "");
        }

        private async Task<WikiPageVersion?> GetLatestVersionAsync(int wikiPageId)
        {
            return await _versionRepo.Query()
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
