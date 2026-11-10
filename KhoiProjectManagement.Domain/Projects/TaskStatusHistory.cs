namespace KhoiProjectManagement.Domain
{
    // Append-only log of every status transition - the data source for cycle time, lead time and
    // cumulative-flow reporting. FromStatus is null for a task's initial status.
    public class TaskStatusHistory : BaseEntity
    {
        public int TaskId { get; set; }
        public virtual ProjectTask Task { get; set; } = null!;
        public string? FromStatus { get; set; }
        public string ToStatus { get; set; } = string.Empty;
        public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
        public int? ChangedById { get; set; }
        public virtual User? ChangedBy { get; set; }
    }
}
