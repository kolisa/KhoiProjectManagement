namespace KhoiProjectManagement.Application
{
    public class LoginResponseDto
    {
        public string Token { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public TeamMemberDto User { get; set; } = null!;
        public List<string> Permissions { get; set; } = new();
        public DateTime ExpiresAt { get; set; }

        // When true, Token/RefreshToken/User/Permissions above are left empty - the frontend must send
        // the caller straight to the reset-password flow using PasswordResetToken instead of treating
        // this as a normal successful login (see AuthService.LoginAsync).
        public bool MustChangePassword { get; set; }
        public string? PasswordResetToken { get; set; }
    }
}
