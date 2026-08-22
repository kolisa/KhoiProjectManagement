namespace KhoiProjectManagement.Models
{
    // A short note tied to a specific prototype/mockup file - distinct from Idea's own general
    // IdeaComment thread (that's discussion about the idea as a whole; this is feedback on one
    // specific attached file). Flat, not threaded, and not pin/coordinate-based markup on the image -
    // a deliberate scope simplification per the user's explicit choice, same reasoning as
    // IdeaComment's own flat-not-threaded design.
    public class IdeaAttachmentAnnotation
    {
        public int Id { get; set; }

        public int IdeaAttachmentId { get; set; }
        public virtual IdeaAttachment IdeaAttachment { get; set; } = null!;

        public int AuthoredBy { get; set; }
        public virtual User Author { get; set; } = null!;

        public string Body { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
