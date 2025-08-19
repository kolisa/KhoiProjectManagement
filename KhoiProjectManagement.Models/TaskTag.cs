namespace KhoiProjectManagement.Models
{
    public class TaskTag
    {
        public int TaskId { get; set; }
        public virtual ProjectTask Task { get; set; } = null!;

        public int TagId { get; set; }
        public virtual Tag Tag { get; set; } = null!;
    }
}
