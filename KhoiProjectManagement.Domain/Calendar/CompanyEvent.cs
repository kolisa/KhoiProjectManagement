namespace KhoiProjectManagement.Domain
{
    // Covers both general company events (holidays, all-hands, socials) and promotion announcements
    // as one lightweight, admin-authored feed - not two entities, since both are "an announcement
    // pinned to a date" with no other behavioral difference (see plan Phase 12.1). Birthdays are never
    // stored here - they're computed at read time from User.DateOfBirth.
    public class CompanyEvent : BaseEntity
    {
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        public DateTime EventDate { get; set; }

        public string EventType { get; set; } = "Event"; // Event, Promotion

        // Set for Promotion entries - identifies who was promoted. Null for general Event entries.
        public int? SubjectUserId { get; set; }
        public virtual User? Subject { get; set; }

        public int CreatedBy { get; set; }
        public virtual User Creator { get; set; } = null!;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
