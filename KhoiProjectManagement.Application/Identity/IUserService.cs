using KhoiProjectManagement.Application;

namespace KhoiProjectManagement.Application
{
    public interface IUserService
    {
        Task<IEnumerable<TeamMemberDto>> GetAllUsersAsync();
        Task<TeamMemberDto?> GetUserByIdAsync(int id);
        Task<TeamMemberDto?> GetUserByEmailAsync(string email);
        Task<TeamMemberDto> CreateUserAsync(CreateUserDto createUserDto);

        // Admin-facing creation path (UsersController.CreateUser) - generates and emails a temp
        // password instead of taking one from the caller, and sets MustChangePassword so the new user
        // is forced through AuthService's reset flow on first login.
        Task<TeamMemberDto> CreateUserWithTempPasswordAsync(CreateAdminUserDto createUserDto);
        Task<bool> UpdateUserAsync(int id, UpdateUserProfileDto updateUserDto);
        Task<bool> DeactivateUserAsync(int id);
        Task<bool> ValidateUserCredentialsAsync(string email, string password);
        Task UpdateLastLoginAsync(int userId);

        // Separate from UpdateUserAsync so profile edits can never also change a user's roles -
        // see UpdateUserProfileDto for why. Returns false if the user or any RoleId doesn't exist.
        Task<bool> AssignRolesAsync(int userId, List<int> roleIds);
    }
}
