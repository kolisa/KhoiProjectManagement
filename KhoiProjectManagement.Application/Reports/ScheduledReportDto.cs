namespace KhoiProjectManagement.Application
{
    public class ScheduledReportDto
    {
        public int Id { get; set; }
        public string ReportType { get; set; } = string.Empty;
        public string Format { get; set; } = string.Empty;
        public string CreatedByName { get; set; } = string.Empty;
        public DateTime NextRunAt { get; set; }
        public DateTime? LastRunAt { get; set; }
        public bool IsActive { get; set; }
    }

    public class CreateScheduledReportDto
    {
        public string ReportType { get; set; } = string.Empty;
        public string Format { get; set; } = string.Empty;
    }
}
