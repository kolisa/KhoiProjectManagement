using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Application;
using KhoiProjectManagement.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MockQueryable;
using MockQueryable.NSubstitute;
using NSubstitute;
using Xunit;

namespace KhoiProjectManagement.UnitTests.Services
{
    public class AuthServiceTests
    {
        private readonly IRepository<RefreshToken> _refreshTokenRepo = Substitute.For<IRepository<RefreshToken>>();
        private readonly IRepository<UserRole> _userRoleRepo = Substitute.For<IRepository<UserRole>>();
        private readonly IRepository<RolePermission> _rolePermissionRepo = Substitute.For<IRepository<RolePermission>>();
        private readonly IRepository<User> _userRepo = Substitute.For<IRepository<User>>();
        private readonly IRepository<PasswordResetToken> _passwordResetTokenRepo = Substitute.For<IRepository<PasswordResetToken>>();
        private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
        private readonly IUserService _userService = Substitute.For<IUserService>();
        private readonly IEmailService _emailService = Substitute.For<IEmailService>();
        private readonly ILogger<AuthService> _logger = Substitute.For<ILogger<AuthService>>();

        private static IConfiguration Config() => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = "unit-test-signing-key-at-least-32-bytes-long!!",
                ["Jwt:Issuer"] = "TestIssuer",
                ["Jwt:Audience"] = "TestAudience",
                ["Jwt:AccessTokenExpiryMinutes"] = "15",
                ["Jwt:RefreshTokenExpiryDays"] = "7",
                ["App:FrontendBaseUrl"] = "http://localhost:3000"
            })
            .Build();

        private AuthService CreateSut() => new(
            _refreshTokenRepo, _userRoleRepo, _rolePermissionRepo, _userRepo, _passwordResetTokenRepo,
            _unitOfWork, _userService, _emailService, Config(), _logger);

        private static TeamMemberDto SampleUser(int id = 1) => new()
        {
            Id = id,
            Name = "Test User",
            Email = "test@khoitech.africa",
            Role = "member",
            Position = "Engineer",
            IsActive = true
        };

        private void SetEmptyRoleAndPermissionQueries()
        {
            _userRoleRepo.Query().Returns(new List<UserRole>().BuildMock());
            _rolePermissionRepo.Query().Returns(new List<RolePermission>().BuildMock());
        }

        [Fact]
        public async Task LoginAsync_WhenCredentialsAreValid_ReturnsTokensAndUpdatesLastLogin()
        {
            var user = SampleUser();
            _userService.ValidateUserCredentialsAsync(user.Email, "correct-password").Returns(true);
            _userService.GetUserByEmailAsync(user.Email).Returns(user);
            SetEmptyRoleAndPermissionQueries();

            var sut = CreateSut();
            var result = await sut.LoginAsync(user.Email, "correct-password");

            Assert.NotNull(result);
            Assert.False(string.IsNullOrEmpty(result!.Token));
            Assert.False(string.IsNullOrEmpty(result.RefreshToken));
            Assert.Equal(user.Id, result.User.Id);
            await _userService.Received(1).UpdateLastLoginAsync(user.Id);
            _refreshTokenRepo.Received(1).Add(Arg.Is<RefreshToken>(rt => rt.UserId == user.Id));
        }

        [Fact]
        public async Task LoginAsync_WhenCredentialsAreInvalid_ReturnsNullAndDoesNotIssueTokens()
        {
            _userService.ValidateUserCredentialsAsync("nobody@khoitech.africa", "wrong").Returns(false);

            var sut = CreateSut();
            var result = await sut.LoginAsync("nobody@khoitech.africa", "wrong");

            Assert.Null(result);
            _refreshTokenRepo.DidNotReceive().Add(Arg.Any<RefreshToken>());
            await _userService.DidNotReceive().UpdateLastLoginAsync(Arg.Any<int>());
        }

        [Fact]
        public async Task LoginAsync_WhenCredentialsValidButUserVanished_ReturnsNull()
        {
            // ValidateUserCredentialsAsync and GetUserByEmailAsync are two separate calls - a user
            // deleted in between must not blow up with a null-reference, it should just fail login.
            _userService.ValidateUserCredentialsAsync("ghost@khoitech.africa", "pw").Returns(true);
            _userService.GetUserByEmailAsync("ghost@khoitech.africa").Returns((TeamMemberDto?)null);

            var sut = CreateSut();
            var result = await sut.LoginAsync("ghost@khoitech.africa", "pw");

            Assert.Null(result);
        }

        [Fact]
        public async Task RefreshAsync_WhenTokenIsUnknown_ReturnsNull()
        {
            _refreshTokenRepo.Query().Returns(new List<RefreshToken>().BuildMock());

            var sut = CreateSut();
            var result = await sut.RefreshAsync("some-random-token-nobody-issued");

            Assert.Null(result);
        }

        [Fact]
        public async Task RefreshAsync_WhenTokenIsRevoked_ReturnsNull()
        {
            var raw = "revoked-raw-token";
            var hash = Hash(raw);
            var revoked = new RefreshToken { Id = 1, UserId = 1, TokenHash = hash, ExpiresAt = DateTime.UtcNow.AddDays(1), RevokedAt = DateTime.UtcNow.AddMinutes(-1) };
            _refreshTokenRepo.Query().Returns(new List<RefreshToken> { revoked }.BuildMock());

            var sut = CreateSut();
            var result = await sut.RefreshAsync(raw);

            Assert.Null(result);
        }

        [Fact]
        public async Task RefreshAsync_WhenTokenIsExpired_ReturnsNull()
        {
            var raw = "expired-raw-token";
            var expired = new RefreshToken { Id = 1, UserId = 1, TokenHash = Hash(raw), ExpiresAt = DateTime.UtcNow.AddDays(-1) };
            _refreshTokenRepo.Query().Returns(new List<RefreshToken> { expired }.BuildMock());

            var sut = CreateSut();
            var result = await sut.RefreshAsync(raw);

            Assert.Null(result);
        }

        [Fact]
        public async Task RefreshAsync_WhenTokenIsActive_IssuesNewTokenAndRevokesOld()
        {
            var raw = "active-raw-token";
            var active = new RefreshToken { Id = 1, UserId = 1, TokenHash = Hash(raw), ExpiresAt = DateTime.UtcNow.AddDays(1) };
            var refreshTokens = new List<RefreshToken> { active };
            _refreshTokenRepo.Query().Returns(_ => refreshTokens.BuildMock());
            _userService.GetUserByIdAsync(1).Returns(SampleUser(1));
            SetEmptyRoleAndPermissionQueries();

            var sut = CreateSut();
            var result = await sut.RefreshAsync(raw);

            Assert.NotNull(result);
            Assert.NotEqual(raw, result!.RefreshToken);
            Assert.NotNull(active.RevokedAt);
        }

        [Fact]
        public async Task LogoutAsync_WhenTokenIsActive_RevokesIt()
        {
            var raw = "logout-raw-token";
            var token = new RefreshToken { Id = 1, UserId = 1, TokenHash = Hash(raw), ExpiresAt = DateTime.UtcNow.AddDays(1) };
            _refreshTokenRepo.Query().Returns(new List<RefreshToken> { token }.BuildMock());

            var sut = CreateSut();
            await sut.LogoutAsync(raw);

            Assert.NotNull(token.RevokedAt);
            await _unitOfWork.Received(1).SaveChangesAsync();
        }

        [Fact]
        public async Task LogoutAsync_WhenTokenIsUnknown_DoesNotThrowOrSave()
        {
            _refreshTokenRepo.Query().Returns(new List<RefreshToken>().BuildMock());

            var sut = CreateSut();
            await sut.LogoutAsync("unknown-token");

            await _unitOfWork.DidNotReceive().SaveChangesAsync();
        }

        [Fact]
        public async Task RequestPasswordResetAsync_WhenEmailIsUnknown_DoesNothingAndDoesNotSendEmail()
        {
            // Must behave identically (outwardly) to the found case - no exception, no email, no DB
            // write - this is the enumeration-safety contract AuthController.ForgotPassword relies on.
            _userService.GetUserByEmailAsync("nobody@khoitech.africa").Returns((TeamMemberDto?)null);

            var sut = CreateSut();
            await sut.RequestPasswordResetAsync("nobody@khoitech.africa");

            _passwordResetTokenRepo.DidNotReceive().Add(Arg.Any<PasswordResetToken>());
            await _emailService.DidNotReceive().SendPasswordResetEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
        }

        [Fact]
        public async Task RequestPasswordResetAsync_WhenEmailSendFails_SwallowsExceptionAfterPersistingToken()
        {
            // Regression test for the deliberate behavior documented in AuthService.cs: a failed SMTP
            // send must never propagate and turn a 204 into a 500 - that would be an email-enumeration
            // side channel (AuthService.cs comment above the catch block).
            var user = SampleUser();
            _userService.GetUserByEmailAsync(user.Email).Returns(user);
            _emailService.SendPasswordResetEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
                .Returns(Task.FromException(new InvalidOperationException("SMTP unreachable")));

            var sut = CreateSut();
            var exception = await Record.ExceptionAsync(() => sut.RequestPasswordResetAsync(user.Email));

            Assert.Null(exception);
            _passwordResetTokenRepo.Received(1).Add(Arg.Any<PasswordResetToken>());
        }

        [Theory]
        [MemberData(nameof(InactiveResetTokenCases))]
        public async Task ResetPasswordAsync_WhenTokenIsInactive_ReturnsFalse(PasswordResetToken? token)
        {
            var tokens = token == null ? new List<PasswordResetToken>() : new List<PasswordResetToken> { token };
            _passwordResetTokenRepo.Query().Returns(tokens.BuildMock());

            var sut = CreateSut();
            var result = await sut.ResetPasswordAsync("whatever-raw-token", "NewP@ssw0rd!");

            Assert.False(result);
        }

        public static IEnumerable<object?[]> InactiveResetTokenCases()
        {
            yield return new object?[] { null }; // unknown token
            yield return new object?[] { new PasswordResetToken { Id = 1, UserId = 1, TokenHash = Hash("whatever-raw-token"), ExpiresAt = DateTime.UtcNow.AddHours(-1) } }; // expired
            yield return new object?[] { new PasswordResetToken { Id = 1, UserId = 1, TokenHash = Hash("whatever-raw-token"), ExpiresAt = DateTime.UtcNow.AddHours(1), UsedAt = DateTime.UtcNow.AddMinutes(-5) } }; // already used
        }

        [Fact]
        public async Task ResetPasswordAsync_WhenTokenIsValid_HashesPasswordAndRevokesAllRefreshTokens()
        {
            const string raw = "valid-reset-token";
            var resetToken = new PasswordResetToken { Id = 1, UserId = 42, TokenHash = Hash(raw), ExpiresAt = DateTime.UtcNow.AddMinutes(30) };
            _passwordResetTokenRepo.Query().Returns(new List<PasswordResetToken> { resetToken }.BuildMock());

            var user = new User { Id = 42, Email = "reset@khoitech.africa", Name = "Reset Me", PasswordHash = "old-hash" };
            _userRepo.Query().Returns(new List<User> { user }.BuildMock());

            var activeTokens = new List<RefreshToken>
            {
                new() { Id = 1, UserId = 42, TokenHash = "a", ExpiresAt = DateTime.UtcNow.AddDays(1) },
                new() { Id = 2, UserId = 42, TokenHash = "b", ExpiresAt = DateTime.UtcNow.AddDays(1) }
            };
            _refreshTokenRepo.Query().Returns(activeTokens.BuildMock());

            var sut = CreateSut();
            var result = await sut.ResetPasswordAsync(raw, "NewP@ssw0rd!");

            Assert.True(result);
            Assert.NotEqual("old-hash", user.PasswordHash);
            Assert.NotNull(resetToken.UsedAt);
            Assert.All(activeTokens, t => Assert.NotNull(t.RevokedAt));
        }

        private static string Hash(string value)
        {
            var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value));
            return Convert.ToHexString(bytes);
        }
    }
}
