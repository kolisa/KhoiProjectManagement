namespace KhoiProjectManagement.Application
{
    public class SystemOverviewEmailSettingsDto
    {
        public bool Enabled { get; set; }
        public DayOfWeek DayOfWeek { get; set; }
        public int Hour { get; set; }
        public int Minute { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
        public string? UpdatedByUserName { get; set; }
    }

    public class UpdateSystemOverviewEmailSettingsDto
    {
        public bool Enabled { get; set; }
        public DayOfWeek DayOfWeek { get; set; }
        public int Hour { get; set; }
        public int Minute { get; set; }
    }
}
