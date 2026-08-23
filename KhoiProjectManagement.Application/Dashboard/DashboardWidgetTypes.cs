namespace KhoiProjectManagement.Application
{
    // The fixed catalog of dashboard widgets - an admin controls which of these are enabled company-
    // wide (DashboardWidgetAllowlist); each user then picks which of the ALLOWED ones to show and in
    // what order (DashboardWidgetPreference). CatalogOrder is the default order for a user who has
    // never customized anything.
    public static class DashboardWidgetTypes
    {
        public const string TotalProjects = "total_projects";
        public const string ActiveProjects = "active_projects";
        public const string TotalTasks = "total_tasks";
        public const string OverdueTasks = "overdue_tasks";
        public const string CompletionRate = "completion_rate";
        public const string RecentTasks = "recent_tasks";
        public const string RecentMentions = "recent_mentions";
        public const string PendingTimesheets = "pending_timesheets";

        public static readonly IReadOnlyList<(string Key, string DisplayName, string Description, int CatalogOrder)> Catalog = new List<(string, string, string, int)>
        {
            (TotalProjects, "Total Projects", "Count of all projects.", 0),
            (ActiveProjects, "Active Projects", "Count of projects currently active.", 1),
            (TotalTasks, "Total Tasks", "Count of all tasks across projects.", 2),
            (OverdueTasks, "Overdue Tasks", "Count of tasks past their due date.", 3),
            (CompletionRate, "Completion Rate", "Percentage of tasks completed.", 4),
            (RecentTasks, "Recent Tasks", "Your five most recently updated tasks.", 5),
            (RecentMentions, "Recent Mentions", "Recent wiki/idea comments that mentioned you.", 6),
            (PendingTimesheets, "Pending Timesheets", "Timesheets awaiting your action (submission or approval).", 7),
        };

        public static bool IsValid(string key) => Catalog.Any(c => c.Key == key);
    }
}
