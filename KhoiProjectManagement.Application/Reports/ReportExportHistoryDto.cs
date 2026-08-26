namespace KhoiProjectManagement.Application
{
    public class ReportExportHistoryDto
    {
        public int Id { get; set; }
        public string ReportType { get; set; } = string.Empty;
        public string Format { get; set; } = string.Empty;
        public string GeneratedByName { get; set; } = string.Empty;
        public DateTime GeneratedAt { get; set; }
        public long FileSizeBytes { get; set; }
    }
}
