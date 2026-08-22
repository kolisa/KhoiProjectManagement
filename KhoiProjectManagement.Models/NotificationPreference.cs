using System.ComponentModel.DataAnnotations;

namespace KhoiProjectManagement.Models
{
    // Opt-out model: the absence of a row for a (UserId, NotificationType) pair means email is
    // enabled by default - so a brand-new notification type never silently goes un-emailed for
    // every existing user just because no row exists yet. Only explicit opt-outs are stored.
    public class NotificationPreference
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        public virtual User User { get; set; } = null!;

        [Required]
        [StringLength(50)]
        public string NotificationType { get; set; } = string.Empty;

        public bool EmailEnabled { get; set; } = true;
    }
}
