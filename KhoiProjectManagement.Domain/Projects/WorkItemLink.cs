namespace KhoiProjectManagement.Domain
{
    // A directed dependency between two work items of the same project. Stored once, in one direction
    // (Source -Type-> Target); the opposite wording ("is blocked by") is derived when reading.
    public class WorkItemLink : BaseEntity
    {
        public int SourceId { get; set; }
        public virtual ProjectTask Source { get; set; } = null!;

        public int TargetId { get; set; }
        public virtual ProjectTask Target { get; set; } = null!;

        public string Type { get; set; } = "Relates"; // Blocks, Relates, Duplicates

        public int CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
