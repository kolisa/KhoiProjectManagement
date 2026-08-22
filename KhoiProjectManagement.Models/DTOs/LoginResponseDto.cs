namespace KhoiProjectManagement.Models.DTOs
{
    public class LoginResponseDto
    {
        public string Token { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public TeamMemberDto User { get; set; } = null!;
        public List<string> Permissions { get; set; } = new();
        public DateTime ExpiresAt { get; set; }
    }
}
