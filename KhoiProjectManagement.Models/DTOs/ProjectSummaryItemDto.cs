namespace KhoiProjectManagement.Models.DTOs
{
    public class ProjectSummaryItemDto
    {
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int TasksCount { get; set; }
        public int CompletedTasks { get; set; }
        public double CompletionRate { get; set; }
    }
}
