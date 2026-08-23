namespace KhoiProjectManagement.Domain
{
    public class PasswordResetToken : BaseEntity
    {
        public int UserId { get; set; }
        public virtual User User { get; set; } = null!;

        // SHA-256 hash of the raw token - the raw value is never persisted.
        public string TokenHash { get; set; } = string.Empty;

        public DateTime ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UsedAt { get; set; }

        public bool IsActive => UsedAt == null && ExpiresAt > DateTime.UtcNow;
    }
}
