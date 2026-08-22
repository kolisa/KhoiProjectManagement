using System.Security.Claims;
using KhoiProjectManagement.Models.DTOs;

namespace KhoiProjectManagementApi.Services
{
    public interface IWikiService
    {
        Task<List<WikiPageSummaryDto>> GetPagesAsync(int spaceId, int? parentPageId, ClaimsPrincipal caller);
        Task<WikiPageDetailDto?> GetPageByIdAsync(int id, ClaimsPrincipal caller);
        Task<WikiPageDetailDto> CreatePageAsync(CreateWikiPageDto dto, ClaimsPrincipal caller);

        // Always inserts a new WikiPageVersion - never mutates an existing one.
        Task<bool> UpdatePageAsync(int id, UpdateWikiPageDto dto, ClaimsPrincipal caller);
        Task<bool> DeletePageAsync(int id, ClaimsPrincipal caller);

        Task<List<WikiPageVersionSummaryDto>?> GetVersionsAsync(int id, ClaimsPrincipal caller);
        Task<WikiPageVersionDetailDto?> GetVersionAsync(int id, int versionNumber, ClaimsPrincipal caller);

        Task<List<WikiCommentDto>?> GetCommentsAsync(int id, ClaimsPrincipal caller);
        Task<WikiCommentDto> AddCommentAsync(int id, CreateWikiCommentDto dto, ClaimsPrincipal caller);

        // Caller must be the comment's author or hold Manage on the page's Space.
        Task<bool> DeleteCommentAsync(int commentId, ClaimsPrincipal caller);
    }
}
