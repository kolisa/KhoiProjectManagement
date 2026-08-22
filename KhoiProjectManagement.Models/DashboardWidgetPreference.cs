using System.ComponentModel.DataAnnotations;

namespace KhoiProjectManagement.Models
{
    // Per-user: which allowed widgets they've chosen to show, and in what order. Absence of a row for
    // a widget means "visible, catalog order" - same opt-out-model reasoning as NotificationPreference,
    // so a brand-new widget added to the catalog shows up for everyone by default instead of being
    // invisible until each user opts in.
    public class DashboardWidgetPreference
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        public virtual User User { get; set; } = null!;

        [Required]
        [StringLength(50)]
        public string WidgetKey { get; set; } = string.Empty;

        public bool IsVisible { get; set; } = true;
        public int SortOrder { get; set; }
    }
}
