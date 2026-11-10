namespace KhoiProjectManagement.Application
{
    public class LabelDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
    }

    public class SetLabelsDto
    {
        // The complete label set for the item, by name. Unknown names are created; omitted ones are removed.
        public List<string> Labels { get; set; } = new();
    }

    public class OverviewSprintDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Goal { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int DaysLeft { get; set; }
        public int TotalPoints { get; set; }
        public int DonePoints { get; set; }
        public int ItemsTotal { get; set; }
        public int ItemsDone { get; set; }
    }

    public class OverviewItemDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public DateTime DueDate { get; set; }
        public bool IsOverdue { get; set; }
        public string? AssignedToName { get; set; }
    }

    public class ProjectOverviewDto
    {
        public int ProjectId { get; set; }
        public string ProjectName { get; set; } = string.Empty;
        public OverviewSprintDto? ActiveSprint { get; set; }
        // Work items by status (Epics and Features are containers and are not counted).
        public Dictionary<string, int> StatusCounts { get; set; } = new();
        public int OpenPoints { get; set; }
        public int OverdueCount { get; set; }
        public int BlockedCount { get; set; }
        public int UnassignedCount { get; set; }
        public List<OverviewItemDto> DueSoon { get; set; } = new();
        public List<OverviewItemDto> MyOpenItems { get; set; } = new();
        public List<ActivityEntryDto> RecentActivity { get; set; } = new();
    }

    public class MyWorkItemDto
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public string ProjectName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public int? StoryPoints { get; set; }
        public DateTime DueDate { get; set; }
        public bool IsOverdue { get; set; }
        public string? SprintName { get; set; }
    }

    public class ActivityEntryDto
    {
        public DateTime When { get; set; }
        public string ActorName { get; set; } = string.Empty;
        // "status" for a recorded status transition, "activity" for everything else logged against the item.
        public string Kind { get; set; } = "activity";
        public string Text { get; set; } = string.Empty;
        // Set on project-wide feeds so a row can say which item it is about.
        public int? ItemId { get; set; }
        public string? ItemTitle { get; set; }
    }
}
