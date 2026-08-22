using System.ComponentModel.DataAnnotations;

namespace KhoiProjectManagement.Models
{
    // Append-only, mirrors WikiPageVersion - each version is its own file on disk (GUID-prefixed
    // StoredPath), so re-uploading with the same display name creates a new version rather than
    // overwriting. No check-in/check-out locking (real SharePoint behavior) - that adds real
    // complexity (lock state, timeout/expiry, forced unlock) for a benefit that doesn't clearly apply
    // yet with a small team; revisit if concurrent-edit conflicts actually happen in practice.
    public class LibraryFileVersion
    {
        public int Id { get; set; }

        public int LibraryFileId { get; set; }
        public virtual LibraryFile LibraryFile { get; set; } = null!;

        public int VersionNumber { get; set; }

        [Required]
        [StringLength(255)]
        public string StoredPath { get; set; } = string.Empty;

        [StringLength(100)]
        public string ContentType { get; set; } = string.Empty;

        public long FileSize { get; set; }

        public string? Comment { get; set; }

        public int UploadedBy { get; set; }
        public virtual User Uploader { get; set; } = null!;

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    }
}
