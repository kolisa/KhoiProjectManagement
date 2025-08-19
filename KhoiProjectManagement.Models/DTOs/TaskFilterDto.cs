namespace KhoiProjectManagement.Models.DTOs
{
    public class TaskFilterDto
    {
        public string? Status { get; set; }
        public string? Priority { get; set; }
        public int? ProjectId { get; set; }
        public int? AssignedToId { get; set; }
        public bool? IsOverdue { get; set; }
        public string? SearchTerm { get; set; }
    }
}
