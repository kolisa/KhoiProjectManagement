namespace KhoiProjectManagement.Domain
{
    public class LoginAuditLog : BaseEntity
    {
        // Nullable - a failed attempt against an email that doesn't match any user has no UserId.
        public int? UserId { get; set; }
        public virtual User? User { get; set; }

        public string EmailAttempted { get; set; } = string.Empty;

        public bool Success { get; set; }

        public string? FailureReason { get; set; }

        public string? IpAddress { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
