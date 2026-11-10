namespace KhoiProjectManagement.Application
{
    public static class SprintStates
    {
        public const string Planned = "Planned";
        public const string Active = "Active";
        public const string Completed = "Completed";
    }

    public class SprintDto
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Goal { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string State { get; set; } = string.Empty;
        public int? Capacity { get; set; }
        public int ItemCount { get; set; }
        // Live sums over the items currently in the sprint.
        public int TotalPoints { get; set; }
        public int DonePoints { get; set; }
        // Snapshots (null until the sprint starts / completes).
        public int? CommittedPoints { get; set; }
        public int? CompletedPoints { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    public class SaveSprintDto
    {
        public string Name { get; set; } = string.Empty;
        public string Goal { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int? Capacity { get; set; }
    }

    public class CompleteSprintDto
    {
        // Where unfinished items go: another planned sprint in the project, or null for the backlog.
        public int? MoveIncompleteToSprintId { get; set; }
    }

    public class AssignSprintDto
    {
        // null removes the item from its sprint (back to the backlog).
        public int? SprintId { get; set; }
    }

    public class BurndownPointDto
    {
        public DateTime Date { get; set; }
        public double Ideal { get; set; }
        // Null for days that haven't happened yet.
        public int? Remaining { get; set; }
    }

    public class BurndownDto
    {
        public int SprintId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public int CommittedPoints { get; set; }
        public List<BurndownPointDto> Points { get; set; } = new();
    }

    public class VelocityPointDto
    {
        public int SprintId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int CommittedPoints { get; set; }
        public int CompletedPoints { get; set; }
    }

    public class VelocityDto
    {
        public List<VelocityPointDto> Sprints { get; set; } = new();
        public double AverageCompleted { get; set; }
    }
}
