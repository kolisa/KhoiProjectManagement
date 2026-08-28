using System.Security.Claims;
using System.Text;
using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Application;
using KhoiProjectManagement.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using MockQueryable;
using MockQueryable.NSubstitute;
using NSubstitute;
using Xunit;

namespace KhoiProjectManagement.UnitTests.Services
{
    // LibraryService is a Space-scoped feature (like Vault and Wiki): permission checks go through
    // IAuthorizationService against a SpacePermissionRequirement, either for the containing Space
    // (uploads into a space) or for the LibraryFile resource itself (read/write/delete on an existing
    // file) - mirrors the mocking pattern in VaultServiceTests.
    //
    // Upload/download methods mix real disk I/O into their business logic (SaveToDiskAsync /
    // ReadFromDiskAsync use File/Directory/FileStream directly, with no IFileSystem abstraction to
    // substitute), so this suite points IConfiguration's FileUpload:LibraryPath at a per-test temp
    // directory rather than mocking the filesystem. That also makes it possible to directly test the
    // "always read by StoredPath, never the display FileName" invariant the service's ReadFromDiskAsync
    // comment calls out, by planting a decoy file under the display name and confirming it's never read.
    public class LibraryServiceTests : IDisposable
    {
        private readonly IRepository<LibraryFile> _fileRepo = Substitute.For<IRepository<LibraryFile>>();
        private readonly IRepository<LibraryFileVersion> _versionRepo = Substitute.For<IRepository<LibraryFileVersion>>();
        private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
        private readonly IAuthorizationService _authorizationService = Substitute.For<IAuthorizationService>();
        private readonly IConfiguration _configuration = Substitute.For<IConfiguration>();
        private readonly string _tempLibraryPath =
            Path.Combine(Path.GetTempPath(), "LibraryServiceTests_" + Guid.NewGuid());

        public LibraryServiceTests()
        {
            _configuration["FileUpload:LibraryPath"].Returns(_tempLibraryPath);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempLibraryPath))
                Directory.Delete(_tempLibraryPath, recursive: true);
        }

        private LibraryService CreateSut() => new(
            _fileRepo, _versionRepo, _unitOfWork, _authorizationService, _configuration);

        private static ClaimsPrincipal CallerWithId(int userId) =>
            new(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) }));

        private void SetAuthorizationResult(bool succeeds) =>
            _authorizationService
                .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object>(), Arg.Any<IEnumerable<IAuthorizationRequirement>>())
                .Returns(succeeds ? AuthorizationResult.Success() : AuthorizationResult.Failed());

        private static IFormFile FakeFile(string fileName, string contentType = "text/plain", long length = 123)
        {
            var file = Substitute.For<IFormFile>();
            file.FileName.Returns(fileName);
            file.ContentType.Returns(contentType);
            file.Length.Returns(length);
            return file;
        }

        // ---- GetFilesAsync ----

        [Fact]
        public async Task GetFilesAsync_WhenCallerLacksReadAccess_ThrowsUnauthorizedAccessException()
        {
            SetAuthorizationResult(succeeds: false);

            var sut = CreateSut();

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sut.GetFilesAsync(10, CallerWithId(1)));
        }

        [Fact]
        public async Task GetFilesAsync_WhenAuthorized_ReturnsOnlyActiveFilesOrderedByFileNameWithCurrentVersionInfo()
        {
            SetAuthorizationResult(succeeds: true);
            var creator = new User { Id = 1, Name = "Alice" };

            var zebra = new LibraryFile { Id = 1, SpaceId = 10, FileName = "zebra.txt", IsActive = true, Creator = creator };
            zebra.Versions.Add(new LibraryFileVersion { VersionNumber = 1, ContentType = "text/plain", FileSize = 10 });
            zebra.Versions.Add(new LibraryFileVersion { VersionNumber = 2, ContentType = "text/plain", FileSize = 20 });

            var apple = new LibraryFile { Id = 2, SpaceId = 10, FileName = "apple.txt", IsActive = true, Creator = creator };
            apple.Versions.Add(new LibraryFileVersion { VersionNumber = 1, ContentType = "text/plain", FileSize = 5 });

            var deleted = new LibraryFile { Id = 3, SpaceId = 10, FileName = "deleted.txt", IsActive = false, Creator = creator };

            _fileRepo.Query().Returns(new List<LibraryFile> { zebra, apple, deleted }.BuildMock());

            var sut = CreateSut();
            var result = await sut.GetFilesAsync(10, CallerWithId(1));

            Assert.Equal(2, result.Count);
            Assert.Equal(new[] { "apple.txt", "zebra.txt" }, result.Select(f => f.FileName));
            var zebraDto = result.Single(f => f.FileName == "zebra.txt");
            Assert.Equal(2, zebraDto.CurrentVersionNumber);
            Assert.Equal(20, zebraDto.FileSize);
        }

        // ---- GetFileByIdAsync ----

        [Fact]
        public async Task GetFileByIdAsync_WhenFileNotFound_ReturnsNullWithoutAuthorizing()
        {
            _fileRepo.Query().Returns(new List<LibraryFile>().BuildMock());

            var sut = CreateSut();
            var result = await sut.GetFileByIdAsync(999, CallerWithId(1));

            Assert.Null(result);
            await _authorizationService.DidNotReceive().AuthorizeAsync(
                Arg.Any<ClaimsPrincipal>(), Arg.Any<object>(), Arg.Any<IEnumerable<IAuthorizationRequirement>>());
        }

        [Fact]
        public async Task GetFileByIdAsync_WhenCallerLacksReadAccess_Throws()
        {
            var file = new LibraryFile { Id = 1, SpaceId = 10, FileName = "doc.txt", IsActive = true };
            _fileRepo.Query().Returns(new List<LibraryFile> { file }.BuildMock());
            SetAuthorizationResult(succeeds: false);

            var sut = CreateSut();

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sut.GetFileByIdAsync(1, CallerWithId(1)));
        }

        // ---- UploadNewFileAsync ----

        [Fact]
        public async Task UploadNewFileAsync_WhenCallerLacksWriteAccess_ThrowsAndNeverAddsFile()
        {
            SetAuthorizationResult(succeeds: false);
            var file = FakeFile("doc.txt");

            var sut = CreateSut();

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sut.UploadNewFileAsync(10, file, CallerWithId(1)));
            _fileRepo.DidNotReceive().Add(Arg.Any<LibraryFile>());
            await _unitOfWork.DidNotReceive().SaveChangesAsync();
        }

        [Fact]
        public async Task UploadNewFileAsync_WhenAuthorized_CreatesFileWithFirstVersion()
        {
            SetAuthorizationResult(succeeds: true);
            LibraryFile? added = null;
            _fileRepo.When(r => r.Add(Arg.Any<LibraryFile>())).Do(ci =>
            {
                added = ci.Arg<LibraryFile>();
                added.Id = 42;
            });
            // LoadFileAsync re-queries after SaveChangesAsync - hand back the same instance Add captured.
            _fileRepo.Query().Returns(_ => new List<LibraryFile> { added! }.BuildMock());
            var file = FakeFile("policy.docx", "application/msword", 2048);

            var sut = CreateSut();
            var result = await sut.UploadNewFileAsync(10, file, CallerWithId(3));

            Assert.Equal("policy.docx", result.FileName);
            Assert.Equal(1, result.CurrentVersionNumber);
            Assert.NotNull(added);
            Assert.Equal(10, added!.SpaceId);
            Assert.Equal(3, added.CreatedBy);
            var version = Assert.Single(added.Versions);
            Assert.Equal(1, version.VersionNumber);
            Assert.Equal(3, version.UploadedBy);
            await _unitOfWork.Received(1).SaveChangesAsync();
        }

        // ---- UploadNewVersionAsync ----

        [Fact]
        public async Task UploadNewVersionAsync_WhenFileNotFound_ReturnsFalse()
        {
            _fileRepo.Query().Returns(new List<LibraryFile>().BuildMock());
            var file = FakeFile("doc.txt");

            var sut = CreateSut();
            var result = await sut.UploadNewVersionAsync(999, file, null, CallerWithId(1));

            Assert.False(result);
            await _authorizationService.DidNotReceive().AuthorizeAsync(
                Arg.Any<ClaimsPrincipal>(), Arg.Any<object>(), Arg.Any<IEnumerable<IAuthorizationRequirement>>());
        }

        [Fact]
        public async Task UploadNewVersionAsync_WhenCallerLacksWriteAccess_ThrowsAndNeverAddsVersion()
        {
            var libraryFile = new LibraryFile { Id = 1, SpaceId = 10, FileName = "doc.txt", IsActive = true };
            libraryFile.Versions.Add(new LibraryFileVersion { VersionNumber = 1, StoredPath = "v1.bin" });
            _fileRepo.Query().Returns(new List<LibraryFile> { libraryFile }.BuildMock());
            SetAuthorizationResult(succeeds: false);
            var file = FakeFile("doc.txt");

            var sut = CreateSut();

            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => sut.UploadNewVersionAsync(1, file, "comment", CallerWithId(1)));
            _versionRepo.DidNotReceive().Add(Arg.Any<LibraryFileVersion>());
        }

        [Fact]
        public async Task UploadNewVersionAsync_WhenAuthorized_IncrementsVersionNumberFromExistingMaxNotCount()
        {
            // Existing version numbers are non-contiguous (1, 5) - if the service ever regressed to a
            // Count-based "next version" instead of Max-based, this would catch it (would compute 3
            // instead of 6).
            var libraryFile = new LibraryFile { Id = 1, SpaceId = 10, FileName = "report.pdf", IsActive = true };
            libraryFile.Versions.Add(new LibraryFileVersion { Id = 1, LibraryFileId = 1, VersionNumber = 1, StoredPath = "old1" });
            libraryFile.Versions.Add(new LibraryFileVersion { Id = 2, LibraryFileId = 1, VersionNumber = 5, StoredPath = "old2" });
            _fileRepo.Query().Returns(new List<LibraryFile> { libraryFile }.BuildMock());
            SetAuthorizationResult(succeeds: true);
            var file = FakeFile("report.pdf");

            var sut = CreateSut();
            var result = await sut.UploadNewVersionAsync(1, file, "updated numbers", CallerWithId(7));

            Assert.True(result);
            _versionRepo.Received(1).Add(Arg.Is<LibraryFileVersion>(v =>
                v.VersionNumber == 6 &&
                v.LibraryFileId == 1 &&
                v.Comment == "updated numbers" &&
                v.UploadedBy == 7));
            await _unitOfWork.Received(1).SaveChangesAsync();
        }

        // ---- GetVersionsAsync ----

        [Fact]
        public async Task GetVersionsAsync_WhenFileNotFound_ReturnsNull()
        {
            _fileRepo.Query().Returns(new List<LibraryFile>().BuildMock());

            var sut = CreateSut();
            var result = await sut.GetVersionsAsync(999, CallerWithId(1));

            Assert.Null(result);
        }

        [Fact]
        public async Task GetVersionsAsync_WhenAuthorized_ReturnsVersionsDescendingWithUnknownFallbackForMissingUploader()
        {
            var uploader = new User { Id = 1, Name = "Bob" };
            var file = new LibraryFile { Id = 1, SpaceId = 10, FileName = "doc.txt", IsActive = true };
            file.Versions.Add(new LibraryFileVersion { VersionNumber = 1, ContentType = "text/plain", FileSize = 5, Uploader = uploader });
            file.Versions.Add(new LibraryFileVersion { VersionNumber = 2, ContentType = "text/plain", FileSize = 8, Uploader = null! });
            _fileRepo.Query().Returns(new List<LibraryFile> { file }.BuildMock());
            SetAuthorizationResult(succeeds: true);

            var sut = CreateSut();
            var result = await sut.GetVersionsAsync(1, CallerWithId(1));

            Assert.NotNull(result);
            Assert.Equal(new[] { 2, 1 }, result!.Select(v => v.VersionNumber));
            Assert.Equal("Unknown", result[0].UploadedByName);
            Assert.Equal("Bob", result[1].UploadedByName);
        }

        // ---- DownloadCurrentAsync ----

        [Fact]
        public async Task DownloadCurrentAsync_WhenFileNotFound_ReturnsNull()
        {
            _fileRepo.Query().Returns(new List<LibraryFile>().BuildMock());

            var sut = CreateSut();
            var result = await sut.DownloadCurrentAsync(999, CallerWithId(1));

            Assert.Null(result);
        }

        [Fact]
        public async Task DownloadCurrentAsync_WhenCallerLacksReadAccess_Throws()
        {
            var file = new LibraryFile { Id = 1, SpaceId = 10, FileName = "doc.txt", IsActive = true };
            file.Versions.Add(new LibraryFileVersion { VersionNumber = 1, StoredPath = "v1.bin" });
            _fileRepo.Query().Returns(new List<LibraryFile> { file }.BuildMock());
            SetAuthorizationResult(succeeds: false);

            var sut = CreateSut();

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sut.DownloadCurrentAsync(1, CallerWithId(1)));
        }

        [Fact]
        public async Task DownloadCurrentAsync_ReadsContentFromStoredPathNotDisplayFileName()
        {
            Directory.CreateDirectory(_tempLibraryPath);
            const string storedPath = "b7f1-actual-content.bin";
            const string displayFileName = "quarterly-report.pdf";
            var expectedBytes = Encoding.UTF8.GetBytes("real stored content");
            File.WriteAllBytes(Path.Combine(_tempLibraryPath, storedPath), expectedBytes);
            // Decoy planted under the display name, with different content - proves the download path
            // never reads from here.
            File.WriteAllBytes(Path.Combine(_tempLibraryPath, displayFileName), Encoding.UTF8.GetBytes("WRONG - must never be read"));

            var file = new LibraryFile { Id = 1, SpaceId = 10, FileName = displayFileName, IsActive = true };
            file.Versions.Add(new LibraryFileVersion { VersionNumber = 1, StoredPath = storedPath, ContentType = "application/pdf" });
            _fileRepo.Query().Returns(new List<LibraryFile> { file }.BuildMock());
            SetAuthorizationResult(succeeds: true);

            var sut = CreateSut();
            var result = await sut.DownloadCurrentAsync(1, CallerWithId(1));

            Assert.NotNull(result);
            Assert.Equal(expectedBytes, result!.Value.Content);
            Assert.Equal(displayFileName, result.Value.FileName);
            Assert.Equal("application/pdf", result.Value.ContentType);
        }

        [Fact]
        public async Task DownloadCurrentAsync_WhenOnlyDisplayFileNameExistsOnDisk_ReturnsNullRatherThanFallingBackToIt()
        {
            Directory.CreateDirectory(_tempLibraryPath);
            const string displayFileName = "quarterly-report.pdf";
            File.WriteAllBytes(Path.Combine(_tempLibraryPath, displayFileName), Encoding.UTF8.GetBytes("decoy content"));

            var file = new LibraryFile { Id = 1, SpaceId = 10, FileName = displayFileName, IsActive = true };
            file.Versions.Add(new LibraryFileVersion { VersionNumber = 1, StoredPath = "never-written.bin", ContentType = "application/pdf" });
            _fileRepo.Query().Returns(new List<LibraryFile> { file }.BuildMock());
            SetAuthorizationResult(succeeds: true);

            var sut = CreateSut();
            var result = await sut.DownloadCurrentAsync(1, CallerWithId(1));

            Assert.Null(result);
        }

        // ---- DownloadVersionAsync ----

        [Fact]
        public async Task DownloadVersionAsync_WhenVersionNumberDoesNotExist_ReturnsNull()
        {
            var file = new LibraryFile { Id = 1, SpaceId = 10, FileName = "doc.txt", IsActive = true };
            file.Versions.Add(new LibraryFileVersion { VersionNumber = 1, StoredPath = "v1.bin" });
            _fileRepo.Query().Returns(new List<LibraryFile> { file }.BuildMock());
            SetAuthorizationResult(succeeds: true);

            var sut = CreateSut();
            var result = await sut.DownloadVersionAsync(1, 99, CallerWithId(1));

            Assert.Null(result);
        }

        // ---- DeleteFileAsync ----

        [Fact]
        public async Task DeleteFileAsync_WhenFileNotFound_ReturnsFalse()
        {
            _fileRepo.Query().Returns(new List<LibraryFile>().BuildMock());

            var sut = CreateSut();
            var result = await sut.DeleteFileAsync(999, CallerWithId(1));

            Assert.False(result);
        }

        [Fact]
        public async Task DeleteFileAsync_WhenCallerLacksManageAccess_ThrowsAndFileRemainsActive()
        {
            var file = new LibraryFile { Id = 1, SpaceId = 10, FileName = "doc.txt", IsActive = true };
            _fileRepo.Query().Returns(new List<LibraryFile> { file }.BuildMock());
            SetAuthorizationResult(succeeds: false);

            var sut = CreateSut();

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sut.DeleteFileAsync(1, CallerWithId(1)));
            Assert.True(file.IsActive);
            await _unitOfWork.DidNotReceive().SaveChangesAsync();
        }

        [Fact]
        public async Task DeleteFileAsync_WhenAuthorized_SoftDeletesRatherThanRemoving()
        {
            var file = new LibraryFile { Id = 1, SpaceId = 10, FileName = "doc.txt", IsActive = true };
            _fileRepo.Query().Returns(new List<LibraryFile> { file }.BuildMock());
            SetAuthorizationResult(succeeds: true);

            var sut = CreateSut();
            var result = await sut.DeleteFileAsync(1, CallerWithId(1));

            Assert.True(result);
            Assert.False(file.IsActive);
            _fileRepo.DidNotReceive().Remove(Arg.Any<LibraryFile>());
            await _unitOfWork.Received(1).SaveChangesAsync();
        }
    }
}
