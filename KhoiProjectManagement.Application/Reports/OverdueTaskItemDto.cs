namespace KhoiProjectManagement.Application
{
    public class OverdueTaskItemDto
    {
        public string Title { get; set; } = string.Empty;
        public string Project { get; set; } = string.Empty;
        public string AssignedTo { get; set; } = string.Empty;
        public DateTime DueDate { get; set; }
        public int DaysOverdue { get; set; }
        public string Priority { get; set; } = string.Empty;
    }
}
