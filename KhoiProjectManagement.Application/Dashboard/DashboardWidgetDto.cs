namespace KhoiProjectManagement.Application
{
    public class DashboardWidgetCatalogEntryDto
    {
        public string WidgetKey { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
    }

    public class SetWidgetAllowlistDto
    {
        public string WidgetKey { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
    }

    public class DashboardWidgetPreferenceDto
    {
        public string WidgetKey { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public bool IsVisible { get; set; }
        public int SortOrder { get; set; }
    }

    public class SetWidgetPreferenceDto
    {
        public string WidgetKey { get; set; } = string.Empty;
        public bool IsVisible { get; set; }
        public int SortOrder { get; set; }
    }
}
