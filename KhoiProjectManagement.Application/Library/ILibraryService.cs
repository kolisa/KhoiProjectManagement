using System.Security.Claims;
using KhoiProjectManagement.Application;
using Microsoft.AspNetCore.Http;

namespace KhoiProjectManagement.Application
{
    public interface ILibraryService
    {
        Task<List<LibraryFileDto>> GetFilesAsync(int spaceId, ClaimsPrincipal caller);
        Task<LibraryFileDto?> GetFileByIdAsync(int id, ClaimsPrincipal caller);

        // Returns null if not found; throws UnauthorizedAccessException if the caller lacks Read.
        Task<(byte[] Content, string ContentType, string FileName)?> DownloadCurrentAsync(int id, ClaimsPrincipal caller);

        Task<LibraryFileDto> UploadNewFileAsync(int spaceId, IFormFile file, ClaimsPrincipal caller);

        // Uploading with the same display name creates a new version rather than overwriting.
        Task<bool> UploadNewVersionAsync(int id, IFormFile file, string? comment, ClaimsPrincipal caller);

        Task<List<LibraryFileVersionDto>?> GetVersionsAsync(int id, ClaimsPrincipal caller);
        Task<(byte[] Content, string ContentType, string FileName)?> DownloadVersionAsync(int id, int versionNumber, ClaimsPrincipal caller);

        Task<bool> DeleteFileAsync(int id, ClaimsPrincipal caller);
    }
}
