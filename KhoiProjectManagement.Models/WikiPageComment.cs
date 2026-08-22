namespace KhoiProjectManagement.Models
{
    public class WikiPageComment
    {
        public int Id { get; set; }

        public int WikiPageId { get; set; }
        public virtual WikiPage WikiPage { get; set; } = null!;

        public int AuthoredBy { get; set; }
        public virtual User Author { get; set; } = null!;

        public string Body { get; set; } = string.Empty;

        // Threaded replies.
        public int? ParentCommentId { get; set; }
        public virtual WikiPageComment? ParentComment { get; set; }
        public virtual ICollection<WikiPageComment> Replies { get; set; } = new List<WikiPageComment>();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Soft delete - so replies to a removed comment don't orphan.
        public bool IsDeleted { get; set; }
    }
}
