using KhoiProjectManagement.Models.DTOs;

namespace KhoiProjectManagementApi.Services
{
    public interface IUserService
    {
        Task<IEnumerable<TeamMemberDto>> GetAllUsersAsync();
        Task<TeamMemberDto?> GetUserByIdAsync(int id);
        Task<TeamMemberDto?> GetUserByEmailAsync(string email);
        Task<TeamMemberDto> CreateUserAsync(CreateUserDto createUserDto);
        Task<bool> UpdateUserAsync(int id, UpdateUserDto updateUserDto);
        Task<bool> DeactivateUserAsync(int id);
        Task<bool> ValidateUserCredentialsAsync(string email, string password);
        Task UpdateLastLoginAsync(int userId);
    }
}
