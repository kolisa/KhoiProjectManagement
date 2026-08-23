using System.Security.Claims;
using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Application;
using KhoiProjectManagement.Domain;
using Microsoft.AspNetCore.Authorization;
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
