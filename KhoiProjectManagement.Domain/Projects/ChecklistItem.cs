namespace KhoiProjectManagement.Domain
{
    // A lightweight to-do line inside a work item, for acceptance criteria / definition-of-done steps
    // that don't warrant a full Subtask.
    public class ChecklistItem : BaseEntity
    {
        public int TaskId { get; set; }
        public virtual ProjectTask Task { get; set; } = null!;

        public string Text { get; set; } = string.Empty;
        public bool IsDone { get; set; }
        public int Position { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int? CompletedBy { get; set; }
    }
}
