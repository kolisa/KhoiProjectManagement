namespace KhoiProjectManagement.Models
{
    // Append-only - never updated or deleted, so history is exact. A new row is inserted on every
    // content edit; renaming a page's Title does not create a version (see WikiPage).
    public class WikiPageVersion
    {
        public int Id { get; set; }

        public int WikiPageId { get; set; }
        public virtual WikiPage WikiPage { get; set; } = null!;

        public int VersionNumber { get; set; }

        public string ContentMarkdown { get; set; } = string.Empty;

        public string? EditSummary { get; set; }

        public int EditedBy { get; set; }
        public virtual User Editor { get; set; } = null!;

        public DateTime EditedAt { get; set; } = DateTime.UtcNow;
    }
}
