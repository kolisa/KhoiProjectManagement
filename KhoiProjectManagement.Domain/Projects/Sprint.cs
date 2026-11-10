namespace KhoiProjectManagement.Domain
{
    // A time-boxed Scrum iteration. Work items join a sprint through ProjectTask.SprintId (no copies),
    // so a project can plan in sprints and run the same items on its Kanban board at once.
    public class Sprint : BaseEntity
    {
        public int ProjectId { get; set; }
        public virtual Project Project { get; set; } = null!;
        public string Name { get; set; } = string.Empty;
        public string Goal { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string State { get; set; } = "Planned"; // Planned, Active, Completed
        // Team capacity in story points, used by planning to flag an over-committed sprint.
        public int? Capacity { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        // Snapshots taken at start / completion so velocity and burndown stay correct after items are
        // later moved out or re-pointed.
        public int? CommittedPoints { get; set; }
        public int? CompletedPoints { get; set; }

        public virtual ICollection<ProjectTask> Tasks { get; set; } = new List<ProjectTask>();
    }
}
