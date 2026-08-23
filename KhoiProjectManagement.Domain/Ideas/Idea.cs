namespace KhoiProjectManagement.Domain
{
    // Company-wide, flat - not Space-scoped, per the user's explicit choice (see plan Phase 11).
    // Every employee can submit and comment; ideas.manage only gates moving Status and converting.
    public class Idea : BaseEntity
    {
        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public int SubmittedBy { get; set; }
        public virtual User Submitter { get; set; } = null!;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public string Status { get; set; } = "Submitted";

        public int? ConvertedProjectId { get; set; }
        public virtual Project? ConvertedProject { get; set; }

        public virtual ICollection<IdeaComment> Comments { get; set; } = new List<IdeaComment>();
    }
}
