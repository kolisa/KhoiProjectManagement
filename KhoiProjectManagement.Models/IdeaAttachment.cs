using System.ComponentModel.DataAnnotations;

namespace KhoiProjectManagement.Models
{
    // Prototype/mockup files attached to an idea - a flat gallery list, not versioned like LibraryFile
    // (a mockup iteration is a new, separately-named file, not a new version of one canonical
    // document - unlike the wiki/library case, there's no single "the look" being replaced over time).
    // StoredFileName is always what's used for disk reads - see IdeaService, which deliberately never
    // repeats the AttachmentsController.DownloadFile bug of reading by the display name.
    public class IdeaAttachment
    {
        public int Id { get; set; }

        public int IdeaId { get; set; }
        public virtual Idea Idea { get; set; } = null!;

        [Required]
        [StringLength(255)]
        public string OriginalFileName { get; set; } = string.Empty;

        [Required]
        [StringLength(255)]
        public string StoredFileName { get; set; } = string.Empty;

        [StringLength(100)]
        public string ContentType { get; set; } = string.Empty;

        public long FileSize { get; set; }

        public int UploadedBy { get; set; }
        public virtual User Uploader { get; set; } = null!;
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        public virtual ICollection<IdeaAttachmentAnnotation> Annotations { get; set; } = new List<IdeaAttachmentAnnotation>();
    }
}
