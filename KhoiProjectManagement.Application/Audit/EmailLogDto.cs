namespace KhoiProjectManagement.Application
{
    // Omits EmailLog.Body - could be large HTML, not needed for an audit list.
    public class EmailLogDto
    {
        public int Id { get; set; }
        public string ToEmail { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string EmailType { get; set; } = string.Empty;
        public DateTime SentAt { get; set; }
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}
