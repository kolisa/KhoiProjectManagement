namespace KhoiProjectManagement.Domain
{
    // Opt-out model: the absence of a row for a (UserId, NotificationType) pair means email is
    // enabled by default - so a brand-new notification type never silently goes un-emailed for
    // every existing user just because no row exists yet. Only explicit opt-outs are stored.
    public class NotificationPreference : BaseEntity
    {
        public int UserId { get; set; }
        public virtual User User { get; set; } = null!;

        public string NotificationType { get; set; } = string.Empty;

        public bool EmailEnabled { get; set; } = true;
    }
}
