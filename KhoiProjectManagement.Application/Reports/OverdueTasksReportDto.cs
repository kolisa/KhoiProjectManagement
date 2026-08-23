namespace KhoiProjectManagement.Application
{
    public class OverdueTasksReportDto
    {
        public string Title { get; set; } = "Overdue Tasks Report";
        public DateTime GeneratedAt { get; set; }
        public int TotalOverdueTasks { get; set; }
        public List<OverdueTaskItemDto> Tasks { get; set; } = new();
    }
}
