namespace KhoiProjectManagement.Application
{
    public class PageVisitLogDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string TabKey { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public int? DurationSeconds { get; set; }
    }

    public class RecordPageVisitDurationDto
    {
        public int DurationSeconds { get; set; }
    }
}
