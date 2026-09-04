namespace KhoiProjectManagement.Domain
{
    // "Page" here means a top-level frontend tab (Projects, Vault, Wiki, ...) - the app has no router,
    // so a tab switch in App.jsx's activeTab state is the closest equivalent of a page visit.
    public class PageVisitLog : BaseEntity
    {
        public int UserId { get; set; }
        public virtual User User { get; set; } = null!;

        public string TabKey { get; set; } = string.Empty;

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        // Set later, once the user navigates away from this tab (or the page unloads) - see
        // PageVisitService.RecordDurationAsync. Null means either the visit is still open or the
        // client never got a chance to report it (a hard crash/force-quit, mainly).
        public int? DurationSeconds { get; set; }
    }
}
