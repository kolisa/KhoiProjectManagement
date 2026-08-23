namespace KhoiProjectManagement.Application
{
    public class ReminderDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime DueAt { get; set; }
        public string Priority { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? Category { get; set; }
        public int AssignedToId { get; set; }
        public string AssignedToName { get; set; } = string.Empty;
        public int CreatedBy { get; set; }
        public string CreatedByName { get; set; } = string.Empty;
        public string Channel { get; set; } = string.Empty;
        public DateTime? SnoozedUntil { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? RecurrenceType { get; set; }
        public DateTime? RecurrenceEndDate { get; set; }
        public int? RecurrenceMaxOccurrences { get; set; }
        public int? RecurrenceParentId { get; set; }
        public int? RelatedProjectId { get; set; }
        public string? RelatedProjectName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // Derived, not stored - "is this overdue right now" depends on the caller's clock, not a value
        // worth persisting and letting go stale.
        public bool IsOverdue => Status == "Pending" && DueAt < DateTime.UtcNow;
    }

    public class CreateReminderDto
    {
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime DueAt { get; set; }
        public string Priority { get; set; } = "medium";
        public string? Category { get; set; }
        public int? AssignedToId { get; set; } // null = self
        public string Channel { get; set; } = "InApp";
        public string? RecurrenceType { get; set; }
        public DateTime? RecurrenceEndDate { get; set; }
        public int? RecurrenceMaxOccurrences { get; set; }
        public int? RelatedProjectId { get; set; }
    }

    public class UpdateReminderDto
    {
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime DueAt { get; set; }
        public string Priority { get; set; } = "medium";
        public string? Category { get; set; }
        public int? AssignedToId { get; set; }
        public string Channel { get; set; } = "InApp";
        public string? RecurrenceType { get; set; }
        public DateTime? RecurrenceEndDate { get; set; }
        public int? RecurrenceMaxOccurrences { get; set; }
        public int? RelatedProjectId { get; set; }
    }

    public class SnoozeReminderDto
    {
        public DateTime SnoozeUntil { get; set; }
    }

    public class BulkReminderActionDto
    {
        public List<int> Ids { get; set; } = new();
    }

    public class BulkRescheduleReminderDto
    {
        public List<int> Ids { get; set; } = new();
        public DateTime DueAt { get; set; }
    }

    public class BulkPriorityReminderDto
    {
        public List<int> Ids { get; set; } = new();
        public string Priority { get; set; } = "medium";
    }

    public class BulkAssignReminderDto
    {
        public List<int> Ids { get; set; } = new();
        public int AssignedToId { get; set; }
    }

    // All optional - an unset filter means "don't filter on this".
    public class ReminderFilterDto
    {
        public string? Search { get; set; }
        public string? Status { get; set; }
        public string? Priority { get; set; }
        public string? Category { get; set; }
        public DateTime? DueFrom { get; set; }
        public DateTime? DueTo { get; set; }
        public int? AssignedToId { get; set; }
        public int? CreatedBy { get; set; }
        public bool? HasRecurrence { get; set; }
        public string? View { get; set; } // "today" | "upcoming" | "overdue" | "completed" | null (all)
    }

    public class ReminderSummaryCountsDto
    {
        public int TotalActive { get; set; }
        public int DueToday { get; set; }
        public int Upcoming { get; set; }
        public int Overdue { get; set; }
        public int Completed { get; set; }
        public int HighPriority { get; set; }
    }
}
