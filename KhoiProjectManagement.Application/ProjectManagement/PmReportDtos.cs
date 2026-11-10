namespace KhoiProjectManagement.Application
{
    public class CumulativeFlowDto
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        // Status keys in stacking order, bottom first (done at the bottom, to-do on top).
        public List<string> Statuses { get; set; } = new();
        public List<CumulativeFlowPointDto> Points { get; set; } = new();
    }

    public class CumulativeFlowPointDto
    {
        public DateTime Date { get; set; }
        // Item count per status at the end of that day, keyed by status.
        public Dictionary<string, int> Counts { get; set; } = new();
    }

    public class FlowMetricsDto
    {
        public int Days { get; set; }
        public int CompletedCount { get; set; }
        public int CompletedPoints { get; set; }
        // Lead time: created -> done. Cycle time: first started -> done (items that were never started
        // - e.g. created straight into Done - are left out of cycle time, not counted as zero).
        public TimeStatsDto LeadTime { get; set; } = new();
        public TimeStatsDto CycleTime { get; set; } = new();
        public List<ThroughputWeekDto> Weeks { get; set; } = new();
    }

    // All figures in days, rounded to one decimal; null when there is nothing to measure.
    public class TimeStatsDto
    {
        public int Samples { get; set; }
        public double? Average { get; set; }
        public double? Median { get; set; }
        public double? P85 { get; set; }
    }

    public class ThroughputWeekDto
    {
        public DateTime WeekStart { get; set; }
        public int Items { get; set; }
        public int Points { get; set; }
    }

    public class WorkloadRowDto
    {
        public int? UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int OpenItems { get; set; }
        public int OpenPoints { get; set; }
        public int InProgress { get; set; }
        public int Blocked { get; set; }
        public int Overdue { get; set; }
        public int DoneLast30Days { get; set; }
    }

    public class CsvExportDto
    {
        public string FileName { get; set; } = string.Empty;
        public byte[] Content { get; set; } = Array.Empty<byte>();
    }
}
