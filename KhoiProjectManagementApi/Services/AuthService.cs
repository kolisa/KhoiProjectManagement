using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using KhoiProjectManagement.Models;
using KhoiProjectManagement.Models.DTOs;
using KhoiProjectManagementApi.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace KhoiProjectManagementApi.Services
{
    public class AuthService : IAuthService
    {
        private readonly ProjectManagementContext _context;
        private readonly IUserService _userService;
        private readonly IConfiguration _configuration;

        public AuthService(ProjectManagementContext context, IUserService userService, IConfiguration configuration)
        {
            _context = context;
            _userService = userService;
            _configuration = configuration;
        }

        public async Task<LoginResponseDto?> LoginAsync(string email, string password)
        {
            var isValid = await _userService.ValidateUserCredentialsAsync(email, password);
            if (!isValid)
                return null;

            var user = await _userService.GetUserByEmailAsync(email);
            if (user == null)
                return null;

            await _userService.UpdateLastLoginAsync(user.Id);

            return await IssueTokensAsync(user);
        }

        public async Task<LoginResponseDto?> RefreshAsync(string refreshToken)
        {
            var tokenHash = Hash(refreshToken);
            var existing = await _context.RefreshTokens.FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash);
            if (existing == null || !existing.IsActive)
                return null;

            var user = await _userService.GetUserByIdAsync(existing.UserId);
            if (user == null)
                return null;

            var response = await IssueTokensAsync(user);

            existing.RevokedAt = DateTime.UtcNow;
            var newTokenHash = Hash(response.RefreshToken);
            existing.ReplacedByTokenId = await _context.RefreshTokens
                .Where(rt => rt.TokenHash == newTokenHash)
                .Select(rt => (int?)rt.Id)
                .FirstOrDefaultAsync();
            await _context.SaveChangesAsync();

            return response;
        }

        public async Task LogoutAsync(string refreshToken)
        {
            var tokenHash = Hash(refreshToken);
            var existing = await _context.RefreshTokens.FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash);
            if (existing != null && existing.RevokedAt == null)
            {
                existing.RevokedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }

        public async Task LogoutAllAsync(int userId)
        {
            var activeTokens = await _context.RefreshTokens
                .Where(rt => rt.UserId == userId && rt.RevokedAt == null)
                .ToListAsync();

            foreach (var token in activeTokens)
            {
                token.RevokedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
        }

        private async Task<LoginResponseDto> IssueTokensAsync(TeamMemberDto user)
        {
            var (permissions, roleIds) = await GetPermissionsAndRoleIdsAsync(user.Id);
            var accessToken = GenerateAccessToken(user, permissions, roleIds);
            var rawRefreshToken = GenerateRawRefreshToken();
            var refreshTokenExpiryDays = int.Parse(_configuration["Jwt:RefreshTokenExpiryDays"] ?? "7");

            _context.RefreshTokens.Add(new RefreshToken
            {
                UserId = user.Id,
                TokenHash = Hash(rawRefreshToken),
                ExpiresAt = DateTime.UtcNow.AddDays(refreshTokenExpiryDays)
            });
            await _context.SaveChangesAsync();

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

        private async Task<(List<string> Permissions, List<int> RoleIds)> GetPermissionsAndRoleIdsAsync(int userId)
        {
            var roleIds = await _context.UserRoles
                .Where(ur => ur.UserId == userId)
                .Select(ur => ur.RoleId)
                .ToListAsync();

            var permissions = await _context.RolePermissions
                .Where(rp => roleIds.Contains(rp.RoleId))
                .Select(rp => rp.Permission.Name)
                .Distinct()
                .ToListAsync();

            return (permissions, roleIds);
        }

        private string GenerateAccessToken(TeamMemberDto user, List<string> permissions, List<int> roleIds)
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
