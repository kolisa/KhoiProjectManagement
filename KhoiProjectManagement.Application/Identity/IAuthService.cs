using KhoiProjectManagement.Application;

namespace KhoiProjectManagement.Application
{
    public interface IAuthService
    {
        Task<LoginResponseDto?> LoginAsync(string email, string password);

        // Validates the opaque refresh token, rotates it (revokes the old one, chains to the new one),
        // and issues a fresh access token + refresh token pair. Returns null if the token is missing,
        // expired, or already revoked.
        Task<LoginResponseDto?> RefreshAsync(string refreshToken);

        Task LogoutAsync(string refreshToken);

        // Revokes every active refresh token for a user - the "kill all sessions" capability needed if
        // a vault credential is suspected compromised.
        Task LogoutAllAsync(int userId);

        // Always completes successfully whether or not the email belongs to a real user - the caller
        // must not be able to distinguish the two cases (user enumeration).
        Task RequestPasswordResetAsync(string email);

        // Returns false if the token is unknown, expired, or already used. On success, also revokes
        // every active refresh token for the user, same as LogoutAllAsync.
        Task<bool> ResetPasswordAsync(string token, string newPassword);
    }
}
