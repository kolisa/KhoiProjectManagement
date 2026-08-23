namespace KhoiProjectManagement.Domain
{
    // Personal by default: AssignedTo is who it's for, CreatedBy is who made it - usually the same
    // person, but a manager with reminders.manage can create one for someone else. Ownership (either
    // field matching the caller) is what grants access with no permission needed - see ReminderService.
    public class Reminder : BaseEntity
    {
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime DueAt { get; set; }
        public string Priority { get; set; } = "medium";
        public string Status { get; set; } = "Pending"; // Pending, Completed, Snoozed
        public string? Category { get; set; }

        public int AssignedToId { get; set; }
        public virtual User AssignedTo { get; set; } = null!;

        public int CreatedBy { get; set; }
        public virtual User Creator { get; set; } = null!;

        public string Channel { get; set; } = "InApp"; // InApp, Email, Both

        public DateTime? SnoozedUntil { get; set; }
        public DateTime? CompletedAt { get; set; }

        // Recurrence: only set on the template/original reminder. Completing one with RecurrenceType
        // set creates the next occurrence (a plain Reminder with RecurrenceParentId pointing back here)
        // rather than mutating this row in place, so completed occurrences stay in history.
        public string? RecurrenceType { get; set; } // Daily, Weekly, Monthly
        public DateTime? RecurrenceEndDate { get; set; }
        public int? RecurrenceMaxOccurrences { get; set; }
        public int? RecurrenceParentId { get; set; }
        public virtual Reminder? RecurrenceParent { get; set; }

        public int? RelatedProjectId { get; set; }
        public virtual Project? RelatedProject { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
