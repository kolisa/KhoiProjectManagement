using System.Security.Claims;
using KhoiProjectManagement.Domain;
using KhoiProjectManagement.Application;

namespace KhoiProjectManagement.Application
{
    public interface IWikiService
    {
        // Returns the caller's effective permission level on the page's Space, or null if the page
        // doesn't exist or the caller has no access at all. Used by WikiHub to gate presence (Read) and
        // the edit lock (Write) without duplicating SpacePermissionResolver plumbing in the hub layer.
        Task<PermissionLevel?> GetMyLevelForPageAsync(int pageId, ClaimsPrincipal caller);

        Task<List<WikiPageSummaryDto>> GetPagesAsync(int spaceId, int? parentPageId, ClaimsPrincipal caller);
        Task<WikiPageDetailDto?> GetPageByIdAsync(int id, ClaimsPrincipal caller);
        Task<WikiPageDetailDto> CreatePageAsync(CreateWikiPageDto dto, ClaimsPrincipal caller);

        // Always inserts a new WikiPageVersion - never mutates an existing one.
        Task<bool> UpdatePageAsync(int id, UpdateWikiPageDto dto, ClaimsPrincipal caller);
        Task<bool> DeletePageAsync(int id, ClaimsPrincipal caller);

        // Re-parents a page (drag onto a different tree node) - stays within the same Space, rejects
        // cycles, appends to the end of the new parent's children.
        Task<bool> MovePageAsync(int id, MoveWikiPageDto dto, ClaimsPrincipal caller);

        // Reorders one full sibling group (drag-and-drop within the current page list) in one call.
        Task<bool> ReorderPagesAsync(int spaceId, int? parentPageId, ReorderWikiPagesDto dto, ClaimsPrincipal caller);

        // Full-replace of a page's labels - reuses the existing shared Tag entity/find-or-create
        // pattern already used by Project/Task tagging.
        Task<bool> SetLabelsAsync(int id, SetWikiPageLabelsDto dto, ClaimsPrincipal caller);

        Task<List<WikiPageVersionSummaryDto>?> GetVersionsAsync(int id, ClaimsPrincipal caller);
        Task<WikiPageVersionDetailDto?> GetVersionAsync(int id, int versionNumber, ClaimsPrincipal caller);

        Task<List<WikiCommentDto>?> GetCommentsAsync(int id, ClaimsPrincipal caller);
        Task<WikiCommentDto> AddCommentAsync(int id, CreateWikiCommentDto dto, ClaimsPrincipal caller);

        // Caller must be the comment's author or hold Manage on the page's Space.
        Task<bool> DeleteCommentAsync(int commentId, ClaimsPrincipal caller);

        // Full-text search over title + current content, restricted to pages the caller currently has
        // at least Read on - never returns a hit for a page whose Space the caller can't see, even if
        // the query text matches, since that would leak the page's existence/content through search.
        Task<List<WikiSearchResultDto>> SearchPagesAsync(string query, ClaimsPrincipal caller);
    }
}
