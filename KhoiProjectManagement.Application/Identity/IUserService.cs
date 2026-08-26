using KhoiProjectManagement.Application;

namespace KhoiProjectManagement.Application
{
    public interface IUserService
    {
        Task<IEnumerable<TeamMemberDto>> GetAllUsersAsync(bool includeInactive = false);
        Task<TeamMemberDto?> GetUserByIdAsync(int id);
        Task<TeamMemberDto?> GetUserByEmailAsync(string email);
        Task<TeamMemberDto> CreateUserAsync(CreateUserDto createUserDto);

        // Admin-facing creation path (UsersController.CreateUser) - generates and emails a temp
        // password instead of taking one from the caller, and sets MustChangePassword so the new user
        // is forced through AuthService's reset flow on first login.
        Task<TeamMemberDto> CreateUserWithTempPasswordAsync(CreateAdminUserDto createUserDto);
        Task<bool> UpdateUserAsync(int id, UpdateUserProfileDto updateUserDto);
        Task<bool> DeactivateUserAsync(int id);

        // The inverse of DeactivateUserAsync - lets an admin undo a lockout without going through the
        // Vault/DB directly. Reusing DeactivateUser's DELETE-as-soft-delete pattern rather than a
        // dedicated "restore" concept elsewhere in this codebase.
        Task<bool> ReactivateUserAsync(int id);

        // Regenerates and re-emails a temp password for a user who hasn't completed their forced
        // first-login reset yet (MustChangePassword still true) - covers "the onboarding email never
        // arrived / got lost" without the admin needing DB access. Throws InvalidOperationException if
        // the user already completed their own password setup, since resending a temp password at
        // that point would silently invalidate a password the user chose themselves.
        Task ResendTempPasswordAsync(int id);
        Task<bool> ValidateUserCredentialsAsync(string email, string password);
        Task UpdateLastLoginAsync(int userId);

        // Separate from UpdateUserAsync so profile edits can never also change a user's roles -
        // see UpdateUserProfileDto for why. Returns false if the user or any RoleId doesn't exist.
        Task<bool> AssignRolesAsync(int userId, List<int> roleIds);
    }
}
