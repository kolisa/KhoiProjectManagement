namespace KhoiProjectManagement.Application
{
    public class LogEntryDto
    {
        public DateTime? Timestamp { get; set; }
        public string Level { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    // Reads Serilog's rolling-daily plain-text log files (Logs/log-{yyyyMMdd}.txt) - a narrow,
    // infrastructure-specific port (direct file-system access) alongside the DB-backed application
    // services, same reasoning as IWikiSearchRepository being its own interface for a
    // provider-specific capability. Implemented in Infrastructure.
    public interface ILogFileService
    {
        // Dates for which a log file currently exists on disk, newest first.
        Task<List<DateOnly>> GetAvailableDatesAsync();

        // The filename is always built server-side from `date` - never accept a client-supplied
        // path. Returns an empty list (not an error) if no file exists for that date.
        Task<List<LogEntryDto>> GetRecentEntriesAsync(DateOnly date, string? levelFilter, int take);
    }
}
