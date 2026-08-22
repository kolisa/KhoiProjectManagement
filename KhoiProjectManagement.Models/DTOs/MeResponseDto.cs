namespace KhoiProjectManagement.Models.DTOs
{
    public class MeResponseDto
    {
        public TeamMemberDto User { get; set; } = null!;
        public List<string> Permissions { get; set; } = new();
    }
}
