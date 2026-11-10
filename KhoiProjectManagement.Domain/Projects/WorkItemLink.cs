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

        // Direction-independent identity of the two items ("low-high"). Unique in the database so the same
        // pair can't be linked twice - in either direction - by concurrent requests.
        public string PairKey { get; set; } = string.Empty;

        public static string KeyFor(int a, int b) => a < b ? $"{a}-{b}" : $"{b}-{a}";

        public int CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
