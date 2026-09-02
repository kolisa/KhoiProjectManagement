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
    }
}
