namespace KhoiProjectManagement.Models
{
    // Deliberately flat, not threaded (no ParentCommentId) - unlike WikiPageComment, an ideas-board
    // discussion is closer to a flat feedback list than a nested conversation. Scope simplification,
    // not an oversight (see plan Phase 11.1).
    public class IdeaComment
    {
        public int Id { get; set; }

        public int IdeaId { get; set; }
        public virtual Idea Idea { get; set; } = null!;

        public int AuthoredBy { get; set; }
        public virtual User Author { get; set; } = null!;

        public string Body { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; }
    }
}
