namespace KhoiProjectManagementApi.Services
{
    // The fixed catalog of notification types that carry an email counterpart - drives both the
    // preferences UI (GET /api/notifications/preferences) and validates PUT bodies against a known
    // set. Extend this list, not ad-hoc strings, whenever a new email-backed notification is added.
    public static class NotificationTypes
    {
        public const string Assignment = "assignment";
        public const string Completion = "completion";
        public const string Overdue = "overdue";
        public const string ProjectCreated = "project_created";
        public const string Mention = "mention";

        public static readonly IReadOnlyList<(string Type, string DisplayName, string Description)> Catalog = new List<(string, string, string)>
        {
            (Assignment, "Task assigned to you", "When you're assigned to a task."),
            (Completion, "Task completed", "When a task assigned to you is marked completed."),
            (Overdue, "Task overdue", "When a task assigned to you passes its due date."),
            (ProjectCreated, "Added to a project", "When you're added as a team member on a new project."),
            (Mention, "Mentioned in a comment", "When someone @mentions you in a wiki page or idea comment."),
        };

        public static bool IsValid(string type) => Catalog.Any(c => c.Type == type);
    }
}
