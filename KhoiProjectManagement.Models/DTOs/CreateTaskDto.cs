namespace KhoiProjectManagement.Models.DTOs
{
    public class CreateTaskDto
    {
        public int ProjectId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Priority { get; set; } = "medium";
        public int? AssignedToId { get; set; }
        public DateTime DueDate { get; set; }
        public List<string> Tags { get; set; } = new();
    }
}
