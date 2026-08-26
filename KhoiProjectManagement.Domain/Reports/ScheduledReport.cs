namespace KhoiProjectManagement.Domain
{
    // Frequency is fixed to weekly for now (matches the one cadence the design spec actually shows) -
    // adding other cadences later is additive, not a breaking change to this shape.
    public class ScheduledReport : BaseEntity
    {
        public string ReportType { get; set; } = string.Empty;
        public string Format { get; set; } = string.Empty;

        public int CreatedByUserId { get; set; }
        public virtual User CreatedByUser { get; set; } = null!;

        public DateTime NextRunAt { get; set; }
        public DateTime? LastRunAt { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
