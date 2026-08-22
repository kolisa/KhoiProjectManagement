using System.ComponentModel.DataAnnotations;

namespace KhoiProjectManagement.Models
{
    // Admin-controlled, company-wide: which widgets from the fixed catalog exist at all. A user's own
    // DashboardWidgetPreference can only select from widgets enabled here - this is the "allow-list",
    // not a per-user setting.
    public class DashboardWidgetAllowlist
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string WidgetKey { get; set; } = string.Empty;

        public bool IsEnabled { get; set; } = true;
    }
}
