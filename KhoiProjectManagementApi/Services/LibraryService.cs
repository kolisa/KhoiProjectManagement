using System.Security.Claims;
using KhoiProjectManagement.Models;
using KhoiProjectManagement.Models.DTOs;
using KhoiProjectManagementApi.Authorization;
using KhoiProjectManagementApi.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace KhoiProjectManagementApi.Services
{
    public class LibraryService : ILibraryService
    {
        private readonly ProjectManagementContext _context;
        private readonly IAuthorizationService _authorizationService;
        private readonly IConfiguration _configuration;

        public LibraryService(
            ProjectManagementContext context,
            IAuthorizationService authorizationService,
            IConfiguration configuration)
        {
            _context = context;
            _authorizationService = authorizationService;
            _configuration = configuration;
        }

        public async Task<List<LibraryFileDto>> GetFilesAsync(int spaceId, ClaimsPrincipal caller)
        {
            await RequireSpaceAccessAsync(spaceId, caller, PermissionLevel.Read);

            var files = await _context.LibraryFiles
                .Include(f => f.Creator)
                .Include(f => f.Versions)
                .Where(f => f.SpaceId == spaceId && f.IsActive)
                .OrderBy(f => f.FileName)
                .ToListAsync();

            return files.Select(MapToDto).ToList();
        }

        public async Task<LibraryFileDto?> GetFileByIdAsync(int id, ClaimsPrincipal caller)
        {
            var file = await LoadFileAsync(id);
            if (file == null)
                return null;

            await AuthorizeFileAsync(file, caller, PermissionLevel.Read);
            return MapToDto(file);
        }

        public async Task<(byte[] Content, string ContentType, string FileName)?> DownloadCurrentAsync(int id, ClaimsPrincipal caller)
        {
            var file = await LoadFileAsync(id);
            if (file == null)
                return null;

            await AuthorizeFileAsync(file, caller, PermissionLevel.Read);

            var current = file.Versions.OrderByDescending(v => v.VersionNumber).First();
            return await ReadFromDiskAsync(current.StoredPath, current.ContentType, file.FileName);
        }

        public async Task<LibraryFileDto> UploadNewFileAsync(int spaceId, IFormFile file, ClaimsPrincipal caller)
        {
            await RequireSpaceAccessAsync(spaceId, caller, PermissionLevel.Write);

            var userId = GetUserId(caller);
            var storedPath = await SaveToDiskAsync(file);

            var libraryFile = new LibraryFile
            {
                SpaceId = spaceId,
                FileName = file.FileName,
                CreatedBy = userId
            };
            libraryFile.Versions.Add(new LibraryFileVersion
            {
                VersionNumber = 1,
                StoredPath = storedPath,
                ContentType = file.ContentType,
                FileSize = file.Length,
                UploadedBy = userId
            });

            _context.LibraryFiles.Add(libraryFile);
            await _context.SaveChangesAsync();

            var saved = await LoadFileAsync(libraryFile.Id);
            return MapToDto(saved!);
        }

        public async Task<bool> UploadNewVersionAsync(int id, IFormFile file, string? comment, ClaimsPrincipal caller)
        {
            var libraryFile = await LoadFileAsync(id);
            if (libraryFile == null)
                return false;

            await AuthorizeFileAsync(libraryFile, caller, PermissionLevel.Write);

            var storedPath = await SaveToDiskAsync(file);
            var nextVersion = libraryFile.Versions.Max(v => v.VersionNumber) + 1;

            _context.LibraryFileVersions.Add(new LibraryFileVersion
            {
                LibraryFileId = libraryFile.Id,
                VersionNumber = nextVersion,
                StoredPath = storedPath,
                ContentType = file.ContentType,
                FileSize = file.Length,
                Comment = comment,
                UploadedBy = GetUserId(caller)
            });

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<LibraryFileVersionDto>?> GetVersionsAsync(int id, ClaimsPrincipal caller)
        {
            var file = await LoadFileAsync(id);
            if (file == null)
                return null;

            await AuthorizeFileAsync(file, caller, PermissionLevel.Read);

            return file.Versions
                .OrderByDescending(v => v.VersionNumber)
                .Select(v => new LibraryFileVersionDto
                {
                    VersionNumber = v.VersionNumber,
                    ContentType = v.ContentType,
                    FileSize = v.FileSize,
                    Comment = v.Comment,
                    UploadedByName = v.Uploader?.Name ?? "Unknown",
                    UploadedAt = v.UploadedAt
                }).ToList();
        }

        public async Task<(byte[] Content, string ContentType, string FileName)?> DownloadVersionAsync(int id, int versionNumber, ClaimsPrincipal caller)
        {
            var file = await LoadFileAsync(id);
            if (file == null)
                return null;

            await AuthorizeFileAsync(file, caller, PermissionLevel.Read);

            var version = file.Versions.FirstOrDefault(v => v.VersionNumber == versionNumber);
            if (version == null)
                return null;

            return await ReadFromDiskAsync(version.StoredPath, version.ContentType, file.FileName);
        }

        public async Task<bool> DeleteFileAsync(int id, ClaimsPrincipal caller)
        {
            var file = await LoadFileAsync(id);
            if (file == null)
                return false;

            await AuthorizeFileAsync(file, caller, PermissionLevel.Manage);

            file.IsActive = false;
            await _context.SaveChangesAsync();
            return true;
        }

        private async Task<LibraryFile?> LoadFileAsync(int id)
        {
            return await _context.LibraryFiles
                .Include(f => f.Creator)
                .Include(f => f.Versions).ThenInclude(v => v.Uploader)
                .FirstOrDefaultAsync(f => f.Id == id && f.IsActive);
        }

        private string GetLibraryPath() => _configuration["FileUpload:LibraryPath"] ?? "wwwroot/library-files";

        private async Task<string> SaveToDiskAsync(IFormFile file)
        {
            var libraryPath = GetLibraryPath();
            var storedFileName = $"{Guid.NewGuid()}_{file.FileName}";
            var filePath = Path.Combine(libraryPath, storedFileName);

            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return storedFileName;
        }

        // Always reads by StoredPath, never by the display FileName - the AttachmentsController
        // DownloadFile bug this module is explicitly designed not to repeat.
        private async Task<(byte[] Content, string ContentType, string FileName)?> ReadFromDiskAsync(string storedPath, string contentType, string displayFileName)
        {
            var filePath = Path.Combine(GetLibraryPath(), storedPath);
            if (!File.Exists(filePath))
                return null;

            var content = await File.ReadAllBytesAsync(filePath);
            return (content, contentType, displayFileName);
        }

        private static LibraryFileDto MapToDto(LibraryFile file)
        {
            var current = file.Versions.OrderByDescending(v => v.VersionNumber).FirstOrDefault();
            return new LibraryFileDto
            {
                Id = file.Id,
                SpaceId = file.SpaceId,
                FileName = file.FileName,
                ContentType = current?.ContentType ?? string.Empty,
                FileSize = current?.FileSize ?? 0,
                CurrentVersionNumber = current?.VersionNumber ?? 0,
                CreatorName = file.Creator?.Name ?? "Unknown",
                CreatedAt = file.CreatedAt,
                LastUploadedAt = current?.UploadedAt
            };
        }

        private async Task AuthorizeFileAsync(LibraryFile file, ClaimsPrincipal caller, PermissionLevel level)
        {
            var result = await _authorizationService.AuthorizeAsync(caller, file, new SpacePermissionRequirement(level));
            if (!result.Succeeded)
                throw new UnauthorizedAccessException($"Caller lacks {level} access to library file {file.Id}.");
        }

        private async Task RequireSpaceAccessAsync(int spaceId, ClaimsPrincipal caller, PermissionLevel level)
        {
            var result = await _authorizationService.AuthorizeAsync(caller, new SpaceReference(spaceId), new SpacePermissionRequirement(level));
            if (!result.Succeeded)
                throw new UnauthorizedAccessException($"Caller lacks {level} access to space {spaceId}.");
        }

        private static int GetUserId(ClaimsPrincipal caller)
        {
            var claim = caller.FindFirst(ClaimTypes.NameIdentifier)
                ?? throw new InvalidOperationException("Caller has no NameIdentifier claim.");
            return int.Parse(claim.Value);
        }
    }
}
