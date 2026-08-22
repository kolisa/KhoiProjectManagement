using KhoiProjectManagement.Models;
using KhoiProjectManagement.Models.DTOs;
using KhoiProjectManagementApi.Data;
using Microsoft.EntityFrameworkCore;

namespace KhoiProjectManagementApi.Services
{
    public class UserService : IUserService
    {
        private readonly ProjectManagementContext _context;

        public UserService(ProjectManagementContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<TeamMemberDto>> GetAllUsersAsync()
        {
            var users = await _context.Users
                .Where(u => u.IsActive)
                .OrderBy(u => u.Name)
                .ToListAsync();

            return users.Select(MapToDto);
        }

        public async Task<TeamMemberDto?> GetUserByIdAsync(int id)
        {
            var user = await _context.Users.FindAsync(id);
            return user == null ? null : MapToDto(user);
        }

        public async Task<TeamMemberDto?> GetUserByEmailAsync(string email)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            return user == null ? null : MapToDto(user);
        }

        public async Task<TeamMemberDto> CreateUserAsync(CreateUserDto createUserDto)
        {
            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == createUserDto.Email);
            if (existingUser != null)
            {
                throw new InvalidOperationException("User with this email already exists");
            }

            var user = new User
            {
                Name = createUserDto.Name,
                Email = createUserDto.Email,
                Role = createUserDto.Role,
                Position = createUserDto.Position,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(createUserDto.Password),
                IsActive = true
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var matchingRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name.ToLower() == createUserDto.Role.ToLower());
            if (matchingRole != null)
            {
                _context.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = matchingRole.Id });
                await _context.SaveChangesAsync();
            }

            return MapToDto(user);
        }

        public async Task<bool> UpdateUserAsync(int id, UpdateUserProfileDto updateUserDto)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
                return false;

            user.Name = updateUserDto.Name;
            user.Email = updateUserDto.Email;
            user.Position = updateUserDto.Position;

            if (!string.IsNullOrEmpty(updateUserDto.Password))
            {
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(updateUserDto.Password);
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> AssignRolesAsync(int userId, List<int> roleIds)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return false;

            var roles = await _context.Roles.Where(r => roleIds.Contains(r.Id)).ToListAsync();
            if (roles.Count != roleIds.Distinct().Count())
                return false; // One or more RoleIds don't exist.

            var existingUserRoles = await _context.UserRoles.Where(ur => ur.UserId == userId).ToListAsync();
            _context.UserRoles.RemoveRange(existingUserRoles);
            _context.UserRoles.AddRange(roles.Select(r => new UserRole { UserId = userId, RoleId = r.Id }));

            // Dual-write the legacy single-string Role column during the transition period. With
            // multiple roles now possible, pick the highest-privilege one (lowest seeded Id) for the
            // legacy field's display purposes - it's a fallback, not the source of truth going forward.
            var primaryRole = roles.OrderBy(r => r.Id).FirstOrDefault();
            if (primaryRole != null)
            {
                user.Role = primaryRole.Name.ToLower();
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeactivateUserAsync(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
                return false;

            user.IsActive = false;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ValidateUserCredentialsAsync(string email, string password)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email && u.IsActive);
            if (user == null)
                return false;

            return BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
        }

        public async Task UpdateLastLoginAsync(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user != null)
            {
                user.LastLoginAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }

        private static TeamMemberDto MapToDto(User user)
        {
            return new TeamMemberDto
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email,
                Role = user.Role,
                Position = user.Position,
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt,
                LastLoginAt = user.LastLoginAt
            };
        }
    }
}
