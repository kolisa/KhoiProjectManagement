using System.Security.Claims;
using System.Text;
using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Application;
using KhoiProjectManagement.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using MockQueryable;
using MockQueryable.NSubstitute;
using NSubstitute;
using Xunit;

namespace KhoiProjectManagement.UnitTests.Services
{
    public class VaultServiceTests
    {
        private readonly IRepository<VaultEntry> _vaultEntryRepo = Substitute.For<IRepository<VaultEntry>>();
        private readonly IRepository<User> _userRepo = Substitute.For<IRepository<User>>();
        private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
        private readonly IVaultEncryptionService _encryptionService = Substitute.For<IVaultEncryptionService>();
        private readonly IVaultAuditService _auditService = Substitute.For<IVaultAuditService>();
        private readonly IAuthorizationService _authorizationService = Substitute.For<IAuthorizationService>();

        private VaultService CreateSut() => new(
            _vaultEntryRepo, _userRepo, _unitOfWork, _encryptionService, _auditService, _authorizationService);

        private static ClaimsPrincipal CallerWithId(int userId) =>
            new(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) }));

        private void SetAuthorizationResult(bool succeeds) =>
            _authorizationService
                .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object>(), Arg.Any<IEnumerable<IAuthorizationRequirement>>())
                .Returns(succeeds ? AuthorizationResult.Success() : AuthorizationResult.Failed());

        [Fact]
        public async Task GetEntryByIdAsync_WhenCallerLacksAccess_ThrowsAndLogsAccessDenied()
        {
            var entry = new VaultEntry { Id = 1, Name = "Prod DB", SpaceId = 10, EncryptedSecret = "cipher", IsActive = true };
            _vaultEntryRepo.Query().Returns(new List<VaultEntry> { entry }.BuildMock());
            SetAuthorizationResult(succeeds: false);

            var sut = CreateSut();
            var caller = CallerWithId(99);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sut.GetEntryByIdAsync(1, caller));
            await _auditService.Received(1).LogAsync(VaultAuditAction.AccessDenied, entry.Id, entry.Name, 99, Arg.Any<string>());
        }

        [Fact]
        public async Task GetEntryByIdAsync_WhenEntryDoesNotExist_ReturnsNullWithoutAuthorizing()
        {
            _vaultEntryRepo.Query().Returns(new List<VaultEntry>().BuildMock());

            var sut = CreateSut();
            var result = await sut.GetEntryByIdAsync(999, CallerWithId(1));

            Assert.Null(result);
            await _authorizationService.DidNotReceive().AuthorizeAsync(
                Arg.Any<ClaimsPrincipal>(), Arg.Any<object>(), Arg.Any<IEnumerable<IAuthorizationRequirement>>());
        }

        [Fact]
        public async Task GetEntryByIdAsync_WhenAuthorized_DecryptsNotesAndLogsViewed()
        {
            var entry = new VaultEntry
            {
                Id = 1,
                Name = "Prod DB",
                SpaceId = 10,
                EncryptedSecret = "cipher-secret",
                EncryptedNotes = "cipher-notes",
                Creator = new User { Id = 5, Name = "Creator" },
                IsActive = true
            };
            _vaultEntryRepo.Query().Returns(new List<VaultEntry> { entry }.BuildMock());
            SetAuthorizationResult(succeeds: true);
            _encryptionService.Decrypt("cipher-notes").Returns("plaintext notes");

            var sut = CreateSut();
            var result = await sut.GetEntryByIdAsync(1, CallerWithId(5));

            Assert.NotNull(result);
            Assert.Equal("plaintext notes", result!.Notes);
            await _auditService.Received(1).LogAsync(VaultAuditAction.Viewed, entry.Id, entry.Name, 5, null);
        }

        [Fact]
        public async Task RevealSecretAsync_WhenAuthorized_DecryptsSecretAndLogsSecretRevealed()
        {
            var entry = new VaultEntry { Id = 2, Name = "API Key", SpaceId = 10, EncryptedSecret = "cipher", IsActive = true };
            _vaultEntryRepo.Query().Returns(new List<VaultEntry> { entry }.BuildMock());
            SetAuthorizationResult(succeeds: true);
            _encryptionService.Decrypt("cipher").Returns("s3cr3t-value");

            var sut = CreateSut();
            var result = await sut.RevealSecretAsync(2, CallerWithId(1));

            Assert.NotNull(result);
            Assert.Equal("s3cr3t-value", result!.SecretValue);
            await _auditService.Received(1).LogAsync(VaultAuditAction.SecretRevealed, entry.Id, entry.Name, 1, null);
        }

        [Fact]
        public async Task CreateEntryAsync_WhenCallerLacksWriteOnSpace_ThrowsAndNeverAddsEntry()
        {
            SetAuthorizationResult(succeeds: false);
            var dto = new CreateVaultEntryDto { Name = "New Secret", SpaceId = 10, SecretValue = "value" };

            var sut = CreateSut();

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sut.CreateEntryAsync(dto, CallerWithId(1)));
            _vaultEntryRepo.DidNotReceive().Add(Arg.Any<VaultEntry>());
            await _unitOfWork.DidNotReceive().SaveChangesAsync();
        }

        [Fact]
        public async Task CreateEntryAsync_WhenAuthorized_EncryptsSecretAndPersists()
        {
            SetAuthorizationResult(succeeds: true);
            _encryptionService.Encrypt("plain-secret").Returns("cipher-secret");
            _userRepo.FindAsync(1).Returns(new User { Id = 1, Name = "Creator" });
            var dto = new CreateVaultEntryDto { Name = "New Secret", SpaceId = 10, SecretValue = "plain-secret" };

            var sut = CreateSut();
            var result = await sut.CreateEntryAsync(dto, CallerWithId(1));

            Assert.Equal("New Secret", result.Name);
            _vaultEntryRepo.Received(1).Add(Arg.Is<VaultEntry>(e => e.EncryptedSecret == "cipher-secret" && e.CreatedBy == 1));
            await _auditService.Received(1).LogAsync(VaultAuditAction.Created, Arg.Any<int>(), "New Secret", 1, null);
        }

        private static IFormFile FakeFile(string content, string fileName)
        {
            var bytes = Encoding.UTF8.GetBytes(content);
            var file = Substitute.For<IFormFile>();
            file.FileName.Returns(fileName);
            file.Length.Returns(bytes.LongLength);
            file.OpenReadStream().Returns(_ => new MemoryStream(bytes));
            return file;
        }

        [Fact]
        public async Task ImportEntriesAsync_WhenCallerLacksWriteOnSpace_ThrowsAndNeverImports()
        {
            SetAuthorizationResult(succeeds: false);
            var file = FakeFile("KEY=value", "secrets.env");

            var sut = CreateSut();

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sut.ImportEntriesAsync(10, file, CallerWithId(1)));
            _vaultEntryRepo.DidNotReceive().AddRange(Arg.Any<IEnumerable<VaultEntry>>());
        }

        [Fact]
        public async Task ImportEntriesAsync_WithEnvFile_EncryptsEachKeyValuePairAndSkipsCommentsAndBlankLines()
        {
            SetAuthorizationResult(succeeds: true);
            _encryptionService.Encrypt(Arg.Any<string>()).Returns(ci => $"cipher:{ci.Arg<string>()}");
            var content = "# a comment\n\nDB_PASSWORD=hunter2\nexport API_KEY=\"abc123\"\n";
            var file = FakeFile(content, "secrets.env");

            var sut = CreateSut();
            var result = await sut.ImportEntriesAsync(10, file, CallerWithId(1));

            Assert.Equal(2, result.Imported);
            Assert.Equal(0, result.Skipped);
            _vaultEntryRepo.Received(1).AddRange(Arg.Is<IEnumerable<VaultEntry>>(entries =>
                entries.Any(e => e.Name == "DB_PASSWORD" && e.EncryptedSecret == "cipher:hunter2") &&
                entries.Any(e => e.Name == "API_KEY" && e.EncryptedSecret == "cipher:abc123")));
            await _unitOfWork.Received(1).SaveChangesAsync();
        }

        [Fact]
        public async Task ImportEntriesAsync_WithNotepadStyleTxtFile_SplitsOnFirstColon()
        {
            SetAuthorizationResult(succeeds: true);
            _encryptionService.Encrypt(Arg.Any<string>()).Returns(ci => $"cipher:{ci.Arg<string>()}");
            var content = "Gmail password: hunter2\nWiFi: My Home Wifi Password 123\nNo separator here\n";
            var file = FakeFile(content, "notes.txt");

            var sut = CreateSut();
            var result = await sut.ImportEntriesAsync(10, file, CallerWithId(1));

            Assert.Equal(2, result.Imported);
            _vaultEntryRepo.Received(1).AddRange(Arg.Is<IEnumerable<VaultEntry>>(entries =>
                entries.Any(e => e.Name == "Gmail password" && e.EncryptedSecret == "cipher:hunter2") &&
                entries.Any(e => e.Name == "WiFi" && e.EncryptedSecret == "cipher:My Home Wifi Password 123")));
        }

        [Fact]
        public async Task ImportEntriesAsync_WithCsvFile_MapsColumnsAndSkipsRowsMissingASecret()
        {
            SetAuthorizationResult(succeeds: true);
            _encryptionService.Encrypt(Arg.Any<string>()).Returns(ci => $"cipher:{ci.Arg<string>()}");
            var content = "name,username,secret,notes\nGitHub,bot@khoi.africa,gh-token-1,CI bot\nNo Secret Here,,,\n";
            var file = FakeFile(content, "secrets.csv");

            var sut = CreateSut();
            var result = await sut.ImportEntriesAsync(10, file, CallerWithId(1));

            Assert.Equal(1, result.Imported);
            Assert.Equal(1, result.Skipped);
            _vaultEntryRepo.Received(1).AddRange(Arg.Is<IEnumerable<VaultEntry>>(entries =>
                entries.Single().Name == "GitHub" &&
                entries.Single().Username == "bot@khoi.africa" &&
                entries.Single().EncryptedSecret == "cipher:gh-token-1"));
        }

        [Fact]
        public async Task ImportEntriesAsync_WithJsonArrayFile_ParsesObjectsCaseInsensitively()
        {
            SetAuthorizationResult(succeeds: true);
            _encryptionService.Encrypt(Arg.Any<string>()).Returns(ci => $"cipher:{ci.Arg<string>()}");
            var content = "[{\"Name\":\"AWS\",\"Secret\":\"aws-secret\",\"Username\":\"deploy\"}]";
            var file = FakeFile(content, "secrets.json");

            var sut = CreateSut();
            var result = await sut.ImportEntriesAsync(10, file, CallerWithId(1));

            Assert.Equal(1, result.Imported);
            _vaultEntryRepo.Received(1).AddRange(Arg.Is<IEnumerable<VaultEntry>>(entries =>
                entries.Single().Name == "AWS" && entries.Single().EncryptedSecret == "cipher:aws-secret"));
        }

        [Fact]
        public async Task ImportEntriesAsync_WhenFileTooLarge_ThrowsWithoutReadingIt()
        {
            SetAuthorizationResult(succeeds: true);
            var file = Substitute.For<IFormFile>();
            file.FileName.Returns("secrets.env");
            file.Length.Returns(3 * 1024 * 1024L);

            var sut = CreateSut();

            await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ImportEntriesAsync(10, file, CallerWithId(1)));
            file.DidNotReceive().OpenReadStream();
        }

        [Fact]
        public async Task DeleteEntryAsync_WhenAuthorized_SoftDeletesRatherThanRemoving()
        {
            var entry = new VaultEntry { Id = 3, Name = "Old Secret", SpaceId = 10, EncryptedSecret = "cipher", IsActive = true };
            _vaultEntryRepo.Query().Returns(new List<VaultEntry> { entry }.BuildMock());
            SetAuthorizationResult(succeeds: true);

            var sut = CreateSut();
            var result = await sut.DeleteEntryAsync(3, CallerWithId(1));

            Assert.True(result);
            Assert.False(entry.IsActive);
            _vaultEntryRepo.DidNotReceive().Remove(Arg.Any<VaultEntry>());
        }

        [Fact]
        public async Task DeleteEntryAsync_WhenEntryAlreadyInactive_ReturnsFalse()
        {
            var entry = new VaultEntry { Id = 4, Name = "Gone", SpaceId = 10, EncryptedSecret = "cipher", IsActive = false };
            _vaultEntryRepo.Query().Returns(new List<VaultEntry> { entry }.BuildMock());

            var sut = CreateSut();
            var result = await sut.DeleteEntryAsync(4, CallerWithId(1));

            Assert.False(result);
        }
    }
}
