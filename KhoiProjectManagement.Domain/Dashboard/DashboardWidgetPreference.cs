namespace KhoiProjectManagement.Domain
{
    // Per-user: which allowed widgets they've chosen to show, and in what order. Absence of a row for
    // a widget means "visible, catalog order" - same opt-out-model reasoning as NotificationPreference,
    // so a brand-new widget added to the catalog shows up for everyone by default instead of being
    // invisible until each user opts in.
    public class DashboardWidgetPreference : BaseEntity
    {
        public int UserId { get; set; }
        public virtual User User { get; set; } = null!;

        public string WidgetKey { get; set; } = string.Empty;

        public bool IsVisible { get; set; } = true;
        public int SortOrder { get; set; }
    }
}
