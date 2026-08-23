namespace KhoiProjectManagement.Application
{
    public class ProjectStatisticsDto
    {
        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int InProgressTasks { get; set; }
        public int TodoTasks { get; set; }
        public int OverdueTasks { get; set; }
        public double CompletionRate { get; set; }
    }
}
