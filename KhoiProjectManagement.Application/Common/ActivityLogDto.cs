namespace KhoiProjectManagement.Application
{
    public class ActivityLogEntryDto
    {
        public int Id { get; set; }
        public string EntityType { get; set; } = string.Empty;
        public int? EntityId { get; set; }
        public string EntityNameSnapshot { get; set; } = string.Empty;
        public string ActorNameSnapshot { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string? Details { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
