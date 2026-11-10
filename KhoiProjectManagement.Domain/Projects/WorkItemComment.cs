namespace KhoiProjectManagement.Domain
{
    // A flat discussion entry on a work item (a ProjectTask). Soft-deleted so a removed comment doesn't
    // silently rewrite the history of a conversation others already replied to.
    public class WorkItemComment : BaseEntity
    {
        public int TaskId { get; set; }
        public virtual ProjectTask Task { get; set; } = null!;

        public int AuthoredBy { get; set; }
        public virtual User Author { get; set; } = null!;

        public string Body { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? EditedAt { get; set; }
        public bool IsDeleted { get; set; }
    }
}
