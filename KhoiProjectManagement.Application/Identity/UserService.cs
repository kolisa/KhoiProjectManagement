using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using KhoiProjectManagement.Application;
using Microsoft.EntityFrameworkCore;

namespace KhoiProjectManagement.Application
{
    public class UserService : IUserService
    {
        private readonly IRepository<User> _userRepo;
        private readonly IRepository<Role> _roleRepo;
        private readonly IRepository<UserRole> _userRoleRepo;
        private readonly IUnitOfWork _unitOfWork;

        public UserService(IRepository<User> userRepo, IRepository<Role> roleRepo, IRepository<UserRole> userRoleRepo, IUnitOfWork unitOfWork)
        {
            _userRepo = userRepo;
            _roleRepo = roleRepo;
            _userRoleRepo = userRoleRepo;
            _unitOfWork = unitOfWork;
        }

        public async Task<IEnumerable<TeamMemberDto>> GetAllUsersAsync()
        {
            var users = await _userRepo.Query()
                .Where(u => u.IsActive)
                .OrderBy(u => u.Name)
                .ToListAsync();

            return users.Select(MapToDto);
        }

        public async Task<TeamMemberDto?> GetUserByIdAsync(int id)
        {
            var user = await _userRepo.FindAsync(id);
            return user == null ? null : MapToDto(user);
        }

        public async Task<TeamMemberDto?> GetUserByEmailAsync(string email)
        {
            var user = await _userRepo.Query().FirstOrDefaultAsync(u => u.Email == email);
            return user == null ? null : MapToDto(user);
        }

        public async Task<TeamMemberDto> CreateUserAsync(CreateUserDto createUserDto)
        {
            var existingUser = await _userRepo.Query().FirstOrDefaultAsync(u => u.Email == createUserDto.Email);
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

            _userRepo.Add(user);
            await _unitOfWork.SaveChangesAsync();

            var matchingRole = await _roleRepo.Query().FirstOrDefaultAsync(r => r.Name.ToLower() == createUserDto.Role.ToLower());
            if (matchingRole != null)
            {
                _userRoleRepo.Add(new UserRole { UserId = user.Id, RoleId = matchingRole.Id });
                await _unitOfWork.SaveChangesAsync();
            }

            return MapToDto(user);
        }

        public async Task<bool> UpdateUserAsync(int id, UpdateUserProfileDto updateUserDto)
        {
            var user = await _userRepo.FindAsync(id);
            if (user == null)
                return false;

            user.Name = updateUserDto.Name;
            user.Email = updateUserDto.Email;
            user.Position = updateUserDto.Position;

            if (!string.IsNullOrEmpty(updateUserDto.Password))
            {
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(updateUserDto.Password);
            }

            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<bool> AssignRolesAsync(int userId, List<int> roleIds)
        {
            var user = await _userRepo.FindAsync(userId);
            if (user == null)
                return false;

            var roles = await _roleRepo.Query().Where(r => roleIds.Contains(r.Id)).ToListAsync();
            if (roles.Count != roleIds.Distinct().Count())
                return false; // One or more RoleIds don't exist.

            var existingUserRoles = await _userRoleRepo.Query().Where(ur => ur.UserId == userId).ToListAsync();
            _userRoleRepo.RemoveRange(existingUserRoles);
            _userRoleRepo.AddRange(roles.Select(r => new UserRole { UserId = userId, RoleId = r.Id }));

            // Dual-write the legacy single-string Role column during the transition period. With
            // multiple roles now possible, pick the highest-privilege one (lowest seeded Id) for the
            // legacy field's display purposes - it's a fallback, not the source of truth going forward.
            var primaryRole = roles.OrderBy(r => r.Id).FirstOrDefault();
            if (primaryRole != null)
            {
                user.Role = primaryRole.Name.ToLower();
            }

            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeactivateUserAsync(int id)
        {
            var user = await _userRepo.FindAsync(id);
            if (user == null)
                return false;

            user.IsActive = false;
            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ValidateUserCredentialsAsync(string email, string password)
        {
            var user = await _userRepo.Query().FirstOrDefaultAsync(u => u.Email == email && u.IsActive);
            if (user == null)
                return false;

            return BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
        }

        public async Task UpdateLastLoginAsync(int userId)
        {
            var user = await _userRepo.FindAsync(userId);
            if (user != null)
            {
                user.LastLoginAt = DateTime.UtcNow;
                await _unitOfWork.SaveChangesAsync();
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
