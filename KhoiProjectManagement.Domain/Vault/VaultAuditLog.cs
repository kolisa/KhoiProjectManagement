namespace KhoiProjectManagement.Domain
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

    public class VaultAuditLog : BaseEntity
    {
        // Nullable so the audit trail survives the entry being deleted.
        public int? VaultEntryId { get; set; }
        public virtual VaultEntry? VaultEntry { get; set; }

        public string EntryNameSnapshot { get; set; } = string.Empty;

        public int UserId { get; set; }
        public virtual User User { get; set; } = null!;

        public VaultAuditAction Action { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public string? IpAddress { get; set; }

        public string? Details { get; set; }
    }
}
