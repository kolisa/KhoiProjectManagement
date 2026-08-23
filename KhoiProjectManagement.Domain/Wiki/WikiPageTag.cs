namespace KhoiProjectManagement.Domain
{
    // Reuses the existing Tag entity (already shared by Project/Task tagging) rather than inventing a
    // separate wiki-specific label concept - same join-table pattern as ProjectTag/TaskTag.
    public class WikiPageTag
    {
        public int WikiPageId { get; set; }
        public virtual WikiPage WikiPage { get; set; } = null!;

        public int TagId { get; set; }
        public virtual Tag Tag { get; set; } = null!;
    }
}
