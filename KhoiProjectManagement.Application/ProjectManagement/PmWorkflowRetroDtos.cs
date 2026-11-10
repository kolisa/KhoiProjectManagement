namespace KhoiProjectManagement.Application
{
    public class WorkflowTransitionDto
    {
        public string From { get; set; } = string.Empty;
        public string To { get; set; } = string.Empty;
        public bool Allowed { get; set; }
    }

    public class WorkflowDto
    {
        public List<string> Statuses { get; set; } = new();
        // Every ordered pair of distinct statuses, with whether the project allows it.
        public List<WorkflowTransitionDto> Transitions { get; set; } = new();
        public bool IsCustomised { get; set; }
    }

    public class SaveWorkflowDto
    {
        // The full matrix as sent by the client; pairs not listed are treated as allowed.
        public List<WorkflowTransitionDto> Transitions { get; set; } = new();
    }

    public static class RetroCategories
    {
        public const string WentWell = "WentWell";
        public const string ToImprove = "ToImprove";
        public const string Action = "Action";
        public static readonly string[] All = { WentWell, ToImprove, Action };
    }

    public class RetroItemDto
    {
        public int Id { get; set; }
        public int SprintId { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public int AuthorId { get; set; }
        public string AuthorName { get; set; } = string.Empty;
        public bool IsDone { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }
    }

    public class SetRetroDoneDto
    {
        public bool Done { get; set; }
    }

    public class SaveRetroItemDto
    {
        public string Category { get; set; } = RetroCategories.WentWell;
        public string Text { get; set; } = string.Empty;
    }
}
