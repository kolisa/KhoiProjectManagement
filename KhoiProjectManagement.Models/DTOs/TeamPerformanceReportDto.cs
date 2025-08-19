namespace KhoiProjectManagement.Models.DTOs
{
    public class TeamPerformanceReportDto
    {
        public string Title { get; set; } = "Team Performance Report";
        public DateTime GeneratedAt { get; set; }
        public List<TeamMemberPerformanceDto> TeamMembers { get; set; } = new();
    }
}
