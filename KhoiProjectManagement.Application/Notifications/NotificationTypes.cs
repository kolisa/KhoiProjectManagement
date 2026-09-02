namespace KhoiProjectManagement.Application
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
        public const string ReminderDue = "reminder_due";
        public const string LoginReminder = "login_reminder";
        public const string WeeklyDigest = "weekly_digest";
        public const string NoDocumentsNudge = "no_documents_nudge";
        public const string DormantUserNudge = "dormant_user_nudge";
        public const string BirthdayGreeting = "birthday_greeting";
        public const string TimesheetSubmitted = "timesheet_submitted";

        public static readonly IReadOnlyList<(string Type, string DisplayName, string Description)> Catalog = new List<(string, string, string)>
        {
            (Assignment, "Task assigned to you", "When you're assigned to a task."),
            (Completion, "Task completed", "When a task assigned to you is marked completed."),
            (Overdue, "Task overdue", "When a task assigned to you passes its due date."),
            (ProjectCreated, "Added to a project", "When you're added as a team member on a new project."),
            (Mention, "Mentioned in a comment", "When someone @mentions you in a wiki page or idea comment."),
            (ReminderDue, "Reminder due", "When a reminder assigned to you reaches its due time."),
            (LoginReminder, "Account setup reminder", "When you haven't finished setting up a newly created account."),
            (WeeklyDigest, "Weekly activity digest", "A weekly summary of your task, project, and Library activity."),
            (NoDocumentsNudge, "No documents uploaded", "When you haven't uploaded any files to the Library."),
            (DormantUserNudge, "Inactivity check-in", "When you haven't logged in for a while."),
            (BirthdayGreeting, "Birthday greeting", "A happy birthday email on your birthday."),
            (TimesheetSubmitted, "Timesheet submitted", "When a timesheet is submitted for your approval (you hold finance.manage)."),
        };

        public static bool IsValid(string type) => Catalog.Any(c => c.Type == type);
    }
}
