namespace KhoiProjectManagement.Models.DTOs
{
    public class LoginResponseDto
    {
        public string Token { get; set; } = string.Empty;
        public TeamMemberDto User { get; set; } = null!;
        public DateTime ExpiresAt { get; set; }
    }
}
