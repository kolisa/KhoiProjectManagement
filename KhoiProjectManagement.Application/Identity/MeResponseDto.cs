namespace KhoiProjectManagement.Application
{
    public class MeResponseDto
    {
        public TeamMemberDto User { get; set; } = null!;
        public List<string> Permissions { get; set; } = new();
    }
}
