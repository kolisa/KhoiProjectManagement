using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using KhoiProjectManagement.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

using Microsoft.Extensions.Configuration;
namespace KhoiProjectManagement.Application
{
    public class AuthService : IAuthService
    {
        private readonly IRepository<RefreshToken> _refreshTokenRepo;
        private readonly IRepository<UserRole> _userRoleRepo;
        private readonly IRepository<UserGroup> _userGroupRepo;
        private readonly IRepository<RolePermission> _rolePermissionRepo;
        private readonly IRepository<User> _userRepo;
        private readonly IRepository<PasswordResetToken> _passwordResetTokenRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IUserService _userService;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthService> _logger;

        public AuthService(
            IRepository<RefreshToken> refreshTokenRepo,
            IRepository<UserRole> userRoleRepo,
            IRepository<UserGroup> userGroupRepo,
            IRepository<RolePermission> rolePermissionRepo,
            IRepository<User> userRepo,
            IRepository<PasswordResetToken> passwordResetTokenRepo,
            IUnitOfWork unitOfWork,
            IUserService userService,
            IEmailService emailService,
            IConfiguration configuration,
            ILogger<AuthService> logger)
        {
            _refreshTokenRepo = refreshTokenRepo;
            _userRoleRepo = userRoleRepo;
            _userGroupRepo = userGroupRepo;
            _rolePermissionRepo = rolePermissionRepo;
            _userRepo = userRepo;
            _passwordResetTokenRepo = passwordResetTokenRepo;
            _unitOfWork = unitOfWork;
            _userService = userService;
            _emailService = emailService;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<LoginResponseDto?> LoginAsync(string email, string password)
        {
            var isValid = await _userService.ValidateUserCredentialsAsync(email, password);
            if (!isValid)
            {
                _logger.LogWarning("Login failed for {Email} - invalid credentials", email);
                return null;
            }

            var user = await _userService.GetUserByEmailAsync(email);
            if (user == null)
            {
                _logger.LogWarning("Login failed for {Email} - user not found after credential check", email);
                return null;
            }

            await _userService.UpdateLastLoginAsync(user.Id);
            _logger.LogInformation("User {UserId} ({Email}) logged in", user.Id, email);

            if (user.MustChangePassword)
            {
                // Identity is already proven (credentials just validated) - hand back a reset token
                // directly instead of emailing one, and issue no session tokens at all until the
                // password is actually changed (ResetPasswordAsync clears this flag on success).
                var rawToken = GenerateRawRefreshToken();
                _passwordResetTokenRepo.Add(new PasswordResetToken
                {
                    UserId = user.Id,
                    TokenHash = Hash(rawToken),
                    ExpiresAt = DateTime.UtcNow.AddHours(1)
                });
                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation("User {UserId} must change password - issuing reset token instead of session", user.Id);

                return new LoginResponseDto
                {
                    MustChangePassword = true,
                    PasswordResetToken = rawToken
                };
            }

            return await IssueTokensAsync(user);
        }

        public async Task<LoginResponseDto?> RefreshAsync(string refreshToken)
        {
            var tokenHash = Hash(refreshToken);
            var existing = await _refreshTokenRepo.Query().FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash);
            if (existing == null || !existing.IsActive)
            {
                // A refresh token that's unknown or already revoked/expired being replayed is worth a
                // signal above Information - could be an expired session or a stolen/reused token.
                _logger.LogWarning("Refresh token rejected - {Reason}", existing == null ? "unknown token" : "inactive (expired or revoked)");
                return null;
            }

            var user = await _userService.GetUserByIdAsync(existing.UserId);
            if (user == null)
                return null;

            var response = await IssueTokensAsync(user);

            existing.RevokedAt = DateTime.UtcNow;
            var newTokenHash = Hash(response.RefreshToken);
            existing.ReplacedByTokenId = await _refreshTokenRepo.Query()
                .Where(rt => rt.TokenHash == newTokenHash)
                .Select(rt => (int?)rt.Id)
                .FirstOrDefaultAsync();
            await _unitOfWork.SaveChangesAsync();

            return response;
        }

        public async Task LogoutAsync(string refreshToken)
        {
            var tokenHash = Hash(refreshToken);
            var existing = await _refreshTokenRepo.Query().FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash);
            if (existing != null && existing.RevokedAt == null)
            {
                existing.RevokedAt = DateTime.UtcNow;
                await _unitOfWork.SaveChangesAsync();
            }
        }

        public async Task LogoutAllAsync(int userId)
        {
            var revokedCount = await RevokeAllRefreshTokensAsync(userId);
            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation("All sessions revoked for user {UserId} ({Count} refresh tokens)", userId, revokedCount);
        }

        public async Task RequestPasswordResetAsync(string email)
        {
            var user = await _userService.GetUserByEmailAsync(email);
            if (user == null)
            {
                // Same outward behavior as the found case - no signal to the caller either way, to
                // avoid leaking which emails are registered.
                _logger.LogInformation("Password reset requested for {Email} - no matching user", email);
                return;
            }

            var rawToken = GenerateRawRefreshToken();
            _passwordResetTokenRepo.Add(new PasswordResetToken
            {
                UserId = user.Id,
                TokenHash = Hash(rawToken),
                ExpiresAt = DateTime.UtcNow.AddHours(1)
            });
            await _unitOfWork.SaveChangesAsync();

            var frontendBaseUrl = (_configuration["App:FrontendBaseUrl"] ?? "http://localhost:3000").TrimEnd('/');
            var resetLink = $"{frontendBaseUrl}/reset-password?token={Uri.EscapeDataString(rawToken)}";

            try
            {
                await _emailService.SendPasswordResetEmailAsync(user.Email, user.Name, resetLink);
                _logger.LogInformation("Password reset email sent for user {UserId}", user.Id);
            }
            catch (Exception ex)
            {
                // Swallowed deliberately: letting this propagate would make the endpoint's response
                // (204 vs 500) distinguish "email exists but SMTP failed" from "email doesn't exist",
                // defeating the no-enumeration contract this endpoint exists to provide. The failure is
                // still fully logged (and audited via EmailLog inside EmailService) for operators.
                _logger.LogError(ex, "Failed to send password reset email for user {UserId}", user.Id);
            }
        }

        public async Task<bool> ResetPasswordAsync(string token, string newPassword)
        {
            var tokenHash = Hash(token);
            var resetToken = await _passwordResetTokenRepo.Query().FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash);
            if (resetToken == null || !resetToken.IsActive)
            {
                _logger.LogWarning("Password reset rejected - {Reason}", resetToken == null ? "unknown token" : "inactive (expired or already used)");
                return false;
            }

            var user = await _userRepo.Query().FirstOrDefaultAsync(u => u.Id == resetToken.UserId);
            if (user == null)
            {
                return false;
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            user.MustChangePassword = false;
            resetToken.UsedAt = DateTime.UtcNow;
            await RevokeAllRefreshTokensAsync(user.Id);

            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation("Password reset completed for user {UserId} - all sessions revoked", user.Id);
            return true;
        }

        private async Task<int> RevokeAllRefreshTokensAsync(int userId)
        {
            var activeTokens = await _refreshTokenRepo.Query()
                .Where(rt => rt.UserId == userId && rt.RevokedAt == null)
                .ToListAsync();

            foreach (var token in activeTokens)
            {
                token.RevokedAt = DateTime.UtcNow;
            }

            return activeTokens.Count;
        }

        private async Task<LoginResponseDto> IssueTokensAsync(TeamMemberDto user)
        {
            var (permissions, roleIds, groupIds) = await GetPermissionsAndRoleIdsAsync(user.Id);
            var accessToken = GenerateAccessToken(user, permissions, roleIds, groupIds);
            var rawRefreshToken = GenerateRawRefreshToken();
            var refreshTokenExpiryDays = int.Parse(_configuration["Jwt:RefreshTokenExpiryDays"] ?? "7");

            _refreshTokenRepo.Add(new RefreshToken
            {
                UserId = user.Id,
                TokenHash = Hash(rawRefreshToken),
                ExpiresAt = DateTime.UtcNow.AddDays(refreshTokenExpiryDays)
            });
            await _unitOfWork.SaveChangesAsync();

            var accessTokenExpiryMinutes = int.Parse(_configuration["Jwt:AccessTokenExpiryMinutes"] ?? "15");

            return new LoginResponseDto
            {
                Token = accessToken,
                RefreshToken = rawRefreshToken,
                User = user,
                Permissions = permissions,
                ExpiresAt = DateTime.UtcNow.AddMinutes(accessTokenExpiryMinutes)
            };
        }

        private async Task<(List<string> Permissions, List<int> RoleIds, List<int> GroupIds)> GetPermissionsAndRoleIdsAsync(int userId)
        {
            var roleIds = await _userRoleRepo.Query()
                .Where(ur => ur.UserId == userId)
                .Select(ur => ur.RoleId)
                .ToListAsync();

            var groupIds = await _userGroupRepo.Query()
                .Where(ug => ug.UserId == userId)
                .Select(ug => ug.GroupId)
                .ToListAsync();

            var permissions = await _rolePermissionRepo.Query()
                .Where(rp => roleIds.Contains(rp.RoleId))
                .Select(rp => rp.Permission.Name)
                .Distinct()
                .ToListAsync();

            return (permissions, roleIds, groupIds);
        }

        private string GenerateAccessToken(TeamMemberDto user, List<string> permissions, List<int> roleIds, List<int> groupIds)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_configuration["Jwt:SecretKey"]!);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Name),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role)
            };
            claims.AddRange(roleIds.Select(id => new Claim("roleId", id.ToString())));
            claims.AddRange(groupIds.Select(id => new Claim("groupId", id.ToString())));
            claims.AddRange(permissions.Select(p => new Claim("permission", p)));

            var accessTokenExpiryMinutes = int.Parse(_configuration["Jwt:AccessTokenExpiryMinutes"] ?? "15");

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(accessTokenExpiryMinutes),
                Issuer = _configuration["Jwt:Issuer"],
                Audience = _configuration["Jwt:Audience"],
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        private static string GenerateRawRefreshToken()
        {
            return Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        }

        private static string Hash(string value)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
            return Convert.ToHexString(bytes);
        }
    }
}
