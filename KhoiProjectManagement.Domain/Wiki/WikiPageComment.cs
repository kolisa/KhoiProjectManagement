namespace KhoiProjectManagement.Domain
{
    public class WikiPageComment : BaseEntity
    {
        public int WikiPageId { get; set; }
        public virtual WikiPage WikiPage { get; set; } = null!;

        public int AuthoredBy { get; set; }
        public virtual User Author { get; set; } = null!;

        public string Body { get; set; } = string.Empty;

        // Anchors this comment to one content block (paragraph/heading/list, split on blank lines -
        // see WikiPageDetail.js) of the page as it existed at comment time, instead of the page as a
        // whole. Null means a general, page-level comment - the original, still-supported behavior.
        // AnchorText is a snapshot of the anchored block's text so the UI can still show what was being
        // discussed even after an edit shifts block indices out from under this anchor - the same
        // "anchor can go stale after an edit" limitation real anchored-comment tools like Confluence
        // and Google Docs have, not something hidden or silently resolved here.
        public int? AnchorBlockIndex { get; set; }
        public string? AnchorText { get; set; }

        // Threaded replies.
        public int? ParentCommentId { get; set; }
        public virtual WikiPageComment? ParentComment { get; set; }
        public virtual ICollection<WikiPageComment> Replies { get; set; } = new List<WikiPageComment>();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Soft delete - so replies to a removed comment don't orphan.
        public bool IsDeleted { get; set; }
    }
}
