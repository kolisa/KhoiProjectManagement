using System.ComponentModel.DataAnnotations;

namespace KhoiProjectManagement.Models
{
    public enum VaultAuditAction
    {
        Created,
        Viewed,
        SecretRevealed,
        Updated,
        Deleted,
        AccessDenied
    }

    public class VaultAuditLog
    {
        public int Id { get; set; }

        // Nullable so the audit trail survives the entry being deleted.
        public int? VaultEntryId { get; set; }
        public virtual VaultEntry? VaultEntry { get; set; }

        [Required]
        [StringLength(200)]
        public string EntryNameSnapshot { get; set; } = string.Empty;

        public int UserId { get; set; }
        public virtual User User { get; set; } = null!;

        public VaultAuditAction Action { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [StringLength(45)]
        public string? IpAddress { get; set; }

        [StringLength(500)]
        public string? Details { get; set; }
    }
}
