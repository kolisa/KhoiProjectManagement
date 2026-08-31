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

            var managerNames = await GetManagerNamesAsync(users.Where(u => u.ManagerId.HasValue).Select(u => u.ManagerId!.Value));
            return users.Select(u => MapToDto(u, managerNames));
        }

        public async Task<TeamMemberDto?> GetUserByIdAsync(int id)
        {
            var user = await _userRepo.FindAsync(id);
            if (user == null) return null;

            var managerNames = await GetManagerNamesAsync(user.ManagerId.HasValue ? new[] { user.ManagerId.Value } : Array.Empty<int>());
            return MapToDto(user, managerNames);
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
            if (user == null) return null;

            var managerNames = await GetManagerNamesAsync(user.ManagerId.HasValue ? new[] { user.ManagerId.Value } : Array.Empty<int>());
            return MapToDto(user, managerNames);
        }

        // One extra query for the whole list rather than one per row - keyed by manager id so
        // GetAllUsersAsync/GetUserByIdAsync/GetUserByEmailAsync can all share it.
        private async Task<Dictionary<int, string>> GetManagerNamesAsync(IEnumerable<int> managerIds)
        {
            var ids = managerIds.Distinct().ToList();
            if (ids.Count == 0) return new Dictionary<int, string>();

            return await _userRepo.Query()
                .Where(u => ids.Contains(u.Id))
                .Select(u => new { u.Id, u.Name })
                .ToDictionaryAsync(u => u.Id, u => u.Name);
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

            await ValidateManagerAsync(createUserDto.ManagerId, excludeUserId: null);

            var tempPassword = TempPasswordGenerator.Generate();

            var user = new User
            {
                Name = createUserDto.Name,
                Email = createUserDto.Email,
                Role = createUserDto.Role,
                Position = createUserDto.Position,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(tempPassword),
                IsActive = true,
                MustChangePassword = true,
                ManagerId = createUserDto.ManagerId,
                DateOfBirth = createUserDto.DateOfBirth
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

            var managerNames = await GetManagerNamesAsync(user.ManagerId.HasValue ? new[] { user.ManagerId.Value } : Array.Empty<int>());
            return MapToDto(user, managerNames);
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

            await ValidateManagerAsync(updateUserDto.ManagerId, excludeUserId: id);

            user.Name = updateUserDto.Name;
            user.Email = updateUserDto.Email;
            user.Position = updateUserDto.Position;
            user.ManagerId = updateUserDto.ManagerId;

            // Nullable-means-"leave unchanged", same convention as Password just below - the edit
            // form never reads the existing DateOfBirth back (see User.DateOfBirth's privacy comment:
            // it's not exposed via TeamMemberDto to arbitrary viewers), so this is the only way to add
            // it later without wiping a previously-set birthday on every unrelated edit.
            if (updateUserDto.DateOfBirth.HasValue)
                user.DateOfBirth = updateUserDto.DateOfBirth.Value;

            if (!string.IsNullOrEmpty(updateUserDto.Password))
            {
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(updateUserDto.Password);
            }

            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        // ManagerId is mutable after creation (unlike Space.ParentSpaceId, which is set once at
        // creation and never re-parentable), so unlike Space this needs an explicit guard: reject a
        // self-report, and reject a change that would make `excludeUserId` its own indirect manager
        // (walking the candidate's existing chain upward). excludeUserId is null on creation, where
        // no cycle is possible yet - a brand-new user can't already be anyone's ancestor.
        private async Task ValidateManagerAsync(int? candidateManagerId, int? excludeUserId)
        {
            if (!candidateManagerId.HasValue)
                return;

            var managerExists = await _userRepo.Query().AnyAsync(u => u.Id == candidateManagerId.Value);
            if (!managerExists)
                throw new InvalidOperationException("The selected manager does not exist.");

            if (!excludeUserId.HasValue)
                return;

            if (candidateManagerId.Value == excludeUserId.Value)
                throw new InvalidOperationException("A user cannot report to themselves.");

            var visited = new HashSet<int>();
            int? currentId = candidateManagerId;
            while (currentId.HasValue)
            {
                if (currentId.Value == excludeUserId.Value)
                    throw new InvalidOperationException("This change would create a circular reporting chain.");
                if (!visited.Add(currentId.Value))
                    break; // Pre-existing bad data guard - never loop forever.

                currentId = await _userRepo.Query()
                    .Where(u => u.Id == currentId.Value)
                    .Select(u => u.ManagerId)
                    .FirstOrDefaultAsync();
            }
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

        private static TeamMemberDto MapToDto(User user, Dictionary<int, string>? managerNames = null)
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
                MustChangePassword = user.MustChangePassword,
                ManagerId = user.ManagerId,
                ManagerName = user.ManagerId.HasValue && managerNames != null
                    ? managerNames.GetValueOrDefault(user.ManagerId.Value)
                    : null
            };
        }
    }
}
