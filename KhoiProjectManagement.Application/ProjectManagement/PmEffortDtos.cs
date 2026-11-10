namespace KhoiProjectManagement.Application
{
    public static class EffortCategories
    {
        public const string AnalysisDesign = "AnalysisDesign";
        public const string DevEffort = "DevEffort";
        public const string DevTesting = "DevTesting";
        public const string Deployment = "Deployment";
        public const string Demo = "Demo";
        public const string Monitoring = "Monitoring";
        public const string Training = "Training";

        // The display order everywhere (item form, sprint drill-down).
        public static readonly string[] All = { AnalysisDesign, DevEffort, DevTesting, Deployment, Demo, Monitoring, Training };
    }

    // One category cell. Points null = N/A.
    public class EffortCellDto
    {
        public string Category { get; set; } = string.Empty;
        public decimal? Points { get; set; }
    }

    // One person's effort on an item, one cell per category (missing = not allocated yet).
    public class PersonEffortDto
    {
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public List<EffortCellDto> Cells { get; set; } = new();
        public decimal Total { get; set; }
    }

    public class WorkItemEffortDto
    {
        public int ItemId { get; set; }
        public List<PersonEffortDto> People { get; set; } = new();
        public decimal Total { get; set; }
    }

    public class SaveWorkItemEffortDto
    {
        public List<SavePersonEffortDto> People { get; set; } = new();
    }

    public class SavePersonEffortDto
    {
        public int UserId { get; set; }
        public List<EffortCellDto> Cells { get; set; } = new();
    }

    // ---- sprint drill-down: person > item > category
    public class SprintItemEffortDto
    {
        public int ItemId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int? ParentId { get; set; }
        public string? ParentTitle { get; set; }
        public decimal Total { get; set; }
        public List<EffortCellDto> Cells { get; set; } = new();
    }

    public class SprintPersonEffortDto
    {
        public int? UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public decimal Total { get; set; }
        // Points on items that are completed.
        public decimal Done { get; set; }
        public List<EffortCellDto> Categories { get; set; } = new();
        public List<SprintItemEffortDto> Items { get; set; } = new();
    }

    public class SprintEffortDto
    {
        public int SprintId { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public decimal Done { get; set; }
        // Items in the sprint that nobody has allocated effort to yet.
        public List<SprintItemEffortDto> Unallocated { get; set; } = new();
        public List<SprintPersonEffortDto> People { get; set; } = new();
    }
}
