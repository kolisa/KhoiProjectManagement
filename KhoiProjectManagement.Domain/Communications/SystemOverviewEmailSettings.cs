namespace KhoiProjectManagement.Domain
{
    // Single-row table (Id=1, seeded via OnModelCreating) controlling the weekly "about the system"
    // email (see SystemOverviewEmailJob in the Quartz project) - modeled as a day of week + time of
    // day rather than a raw cron string, since that's genuinely all this schedule is; the Quartz
    // project is the only place that ever turns this into cron syntax (see JobRescheduler). Admin-
    // editable from Settings > System Overview Email, gated by the email.manage_overview permission.
    public class SystemOverviewEmailSettings : BaseEntity
    {
        public bool Enabled { get; set; } = true;
        public DayOfWeek DayOfWeek { get; set; } = DayOfWeek.Friday;
        public int Hour { get; set; } = 10;
        public int Minute { get; set; } = 0;
        public DateTime UpdatedAtUtc { get; set; }
        public int? UpdatedByUserId { get; set; }
        public User? UpdatedByUser { get; set; }
    }
}
