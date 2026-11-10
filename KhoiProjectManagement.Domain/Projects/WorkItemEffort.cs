namespace KhoiProjectManagement.Domain
{
    // One person's planned effort on one category of one work item (Analysis & design, Dev effort, Dev testing,
    // Deployment, Demo, Monitoring, Training). Points == null means the category is "N/A" for that person on that
    // item, which is different from 0 (counted, but nothing to do). A person's sprint points are the sum of these
    // over the items in the sprint.
    public class WorkItemEffort : BaseEntity
    {
        public int TaskId { get; set; }
        public virtual ProjectTask Task { get; set; } = null!;

        public int UserId { get; set; }
        public virtual User User { get; set; } = null!;

        public string Category { get; set; } = string.Empty;
        public decimal? Points { get; set; }
    }
}
