namespace KhoiProjectManagement.Models.DTOs
{
    public class CreateProjectDto
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Priority { get; set; } = "medium";
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public List<int> TeamMemberIds { get; set; } = new();
        public List<string> Tags { get; set; } = new();
    }
}
