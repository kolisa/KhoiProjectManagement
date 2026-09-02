namespace KhoiProjectManagement.Application
{
    public class UpdateTaskDto
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public string Type { get; set; } = "Task";
        public int? AssignedToId { get; set; }
        public DateTime DueDate { get; set; }
        public List<string> Tags { get; set; } = new();
    }
}
