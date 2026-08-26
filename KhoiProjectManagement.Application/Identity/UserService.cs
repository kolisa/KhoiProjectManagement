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
        private readonly IEmailService _emailService;

        public UserService(IRepository<User> userRepo, IRepository<Role> roleRepo, IRepository<UserRole> userRoleRepo, IUnitOfWork unitOfWork, IEmailService emailService)
        {
            _userRepo = userRepo;
            _roleRepo = roleRepo;
            _userRoleRepo = userRoleRepo;
            _unitOfWork = unitOfWork;
            _emailService = emailService;
        }

        public async Task<IEnumerable<TeamMemberDto>> GetAllUsersAsync(bool includeInactive = false)
        {
            var query = _userRepo.Query();
            if (!includeInactive)
                query = query.Where(u => u.IsActive);

            var users = await query.OrderBy(u => u.Name).ToListAsync();

            return users.Select(MapToDto);
        }

        public async Task<TeamMemberDto?> GetUserByIdAsync(int id)
        {
            var user = await _userRepo.FindAsync(id);
            return user == null ? null : MapToDto(user);
        }

        public async Task<TeamMemberDto?> GetUserByEmailAsync(string email)
        {
            // Case-insensitive - email addresses are conventionally treated as case-insensitive
            // everywhere (login forms, "forgot password", etc.), but Postgres' default column
            // collation is case-sensitive, so a plain == silently fails to find an existing user for
            // any differently-cased but otherwise-correct email (e.g. the seeded
            // kholisa@khoitech.Africa vs someone typing kholisa@khoitech.africa) - this was reported
            // as "wrong password" even with the right password, since the user lookup itself failed
            // before the password was ever checked.
            var user = await _userRepo.Query().FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());
            return user == null ? null : MapToDto(user);
        }

        public async Task<TeamMemberDto> CreateUserAsync(CreateUserDto createUserDto)
        {
            var existingUser = await _userRepo.Query().FirstOrDefaultAsync(u => u.Email.ToLower() == createUserDto.Email.ToLower());
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

            await AssignMatchingRoleAsync(user.Id, createUserDto.Role);

            return MapToDto(user);
        }

        public async Task<TeamMemberDto> CreateUserWithTempPasswordAsync(CreateAdminUserDto createUserDto)
        {
            var existingUser = await _userRepo.Query().FirstOrDefaultAsync(u => u.Email.ToLower() == createUserDto.Email.ToLower());
            if (existingUser != null)
            {
                throw new InvalidOperationException("User with this email already exists");
            }

            var tempPassword = TempPasswordGenerator.Generate();

            var user = new User
            {
                Name = createUserDto.Name,
                Email = createUserDto.Email,
                Role = createUserDto.Role,
                Position = createUserDto.Position,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(tempPassword),
                IsActive = true,
                MustChangePassword = true
            };

            _userRepo.Add(user);
            await _unitOfWork.SaveChangesAsync();

            await AssignMatchingRoleAsync(user.Id, createUserDto.Role);

            try
            {
                await _emailService.SendTemporaryPasswordEmailAsync(user.Email, user.Name, tempPassword);
            }
            catch
            {
                // The account is already created and usable via the forgot-password flow even if this
                // send fails - already logged to EmailLog by EmailService, same as every other
                // post-creation email in this codebase (e.g. ProjectService's project-created email).
            }

            return MapToDto(user);
        }

        private async Task AssignMatchingRoleAsync(int userId, string roleName)
        {
            var matchingRole = await _roleRepo.Query().FirstOrDefaultAsync(r => r.Name.ToLower() == roleName.ToLower());
            if (matchingRole != null)
            {
                _userRoleRepo.Add(new UserRole { UserId = userId, RoleId = matchingRole.Id });
                await _unitOfWork.SaveChangesAsync();
            }
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

        public async Task<bool> ReactivateUserAsync(int id)
        {
            var user = await _userRepo.FindAsync(id);
            if (user == null)
                return false;

            user.IsActive = true;
            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task ResendTempPasswordAsync(int id)
        {
            var user = await _userRepo.FindAsync(id);
            if (user == null)
                throw new KeyNotFoundException("User not found");

            if (!user.MustChangePassword)
                throw new InvalidOperationException("This user has already set their own password.");

            var tempPassword = TempPasswordGenerator.Generate();
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(tempPassword);
            await _unitOfWork.SaveChangesAsync();

            // Deliberately not swallowed like the creation-time send (CreateUserWithTempPasswordAsync) -
            // resending only happens because the first email is believed lost, so a second silent
            // failure here should surface to the admin rather than leave them thinking it worked.
            await _emailService.SendTemporaryPasswordEmailAsync(user.Email, user.Name, tempPassword);
        }

        public async Task<bool> ValidateUserCredentialsAsync(string email, string password)
        {
            // Case-insensitive on the email - see GetUserByEmailAsync's comment above. The password
            // itself stays case-sensitive (BCrypt.Verify below), which is correct - only the email
            // lookup was ever the problem.
            var user = await _userRepo.Query().FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower() && u.IsActive);
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
                LastLoginAt = user.LastLoginAt,
                MustChangePassword = user.MustChangePassword
            };
        }
    }
}
