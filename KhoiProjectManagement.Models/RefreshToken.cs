using System.ComponentModel.DataAnnotations;

namespace KhoiProjectManagement.Models
{
    public class RefreshToken
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        public virtual User User { get; set; } = null!;

        // SHA-256 hash of the raw token - the raw value is never persisted.
        [Required]
        [StringLength(200)]
        public string TokenHash { get; set; } = string.Empty;

        public DateTime ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? RevokedAt { get; set; }

        // Set when this token is rotated out in favor of a new one, chaining the rotation history.
        public int? ReplacedByTokenId { get; set; }

        public bool IsActive => RevokedAt == null && ExpiresAt > DateTime.UtcNow;
    }
}
