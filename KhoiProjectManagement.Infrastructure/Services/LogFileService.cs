using System.Text.RegularExpressions;
using KhoiProjectManagement.Application;
using Microsoft.Extensions.Configuration;

namespace KhoiProjectManagement.Infrastructure.Services
{
    public partial class LogFileService : ILogFileService
    {
        // Matches the Serilog outputTemplate's leading timestamp, e.g. "2026-08-27 14:03:22.101 +02:00 [ERR] ..."
        // - a line that doesn't start with this is a continuation of the previous entry (an exception
        // stack trace has no timestamp/level of its own).
        [GeneratedRegex(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d+ [+-]\d{2}:\d{2} \[(\w+)\]\s?")]
        private static partial Regex EntryStartRegex();

        // Grown up to this many bytes from the end of the file while searching for enough matching
        // entries - a hard ceiling so a huge file (or a levelFilter that matches almost nothing) can't
        // make a single request read the whole thing into memory.
        private const long MaxWindowBytes = 20 * 1024 * 1024;
        private const long InitialWindowBytes = 2 * 1024 * 1024;

        private readonly string _logDirectory;

        public LogFileService(IConfiguration configuration)
        {
            // Deliberately its own "LogViewer" section, not nested under "Logging" (ASP.NET Core's own
            // LogLevel config) or "Serilog" (would mean parsing Serilog:WriteTo's array to find the
            // File sink's Args:path, fragile if sink order ever changes) - this is the one value the two
            // need to agree on, kept independent and explicit.
            _logDirectory = configuration["LogViewer:Directory"] ?? "Logs";
        }

        public Task<List<DateOnly>> GetAvailableDatesAsync()
        {
            if (!Directory.Exists(_logDirectory))
                return Task.FromResult(new List<DateOnly>());

            var dates = new List<DateOnly>();
            foreach (var path in Directory.GetFiles(_logDirectory, "log-*.txt"))
            {
                var stem = Path.GetFileNameWithoutExtension(path); // "log-20260827"
                var datePart = stem.Length > 4 ? stem[4..] : string.Empty;
                if (DateOnly.TryParseExact(datePart, "yyyyMMdd", out var date))
                    dates.Add(date);
            }

            dates.Sort((a, b) => b.CompareTo(a));
            return Task.FromResult(dates.Take(14).ToList());
        }

        public async Task<List<LogEntryDto>> GetRecentEntriesAsync(DateOnly date, string? levelFilter, int take)
        {
            var path = Path.Combine(_logDirectory, $"log-{date:yyyyMMdd}.txt");
            if (!File.Exists(path))
                return new List<LogEntryDto>();

            var wantedLevels = LevelsFor(levelFilter);
            var windowBytes = InitialWindowBytes;

            while (true)
            {
                var entries = await ReadTailEntriesAsync(path, windowBytes);
                var matching = wantedLevels == null
                    ? entries
                    : entries.Where(e => wantedLevels.Contains(e.Level)).ToList();

                var reachedStartOfFile = windowBytes >= new FileInfo(path).Length;
                if (matching.Count >= take || reachedStartOfFile || windowBytes >= MaxWindowBytes)
                {
                    matching.Reverse(); // ReadTailEntriesAsync returns oldest-first within the window
                    return matching.Take(take).ToList();
                }

                windowBytes *= 2;
            }
        }

        private static HashSet<string>? LevelsFor(string? levelFilter) => levelFilter?.ToLowerInvariant() switch
        {
            "warning" => new HashSet<string> { "WRN" },
            "error" => new HashSet<string> { "ERR", "FTL" },
            _ => null,
        };

        // Reads the last `windowBytes` of the file (or the whole file if smaller), parses it into
        // entries, oldest-first. Requires FileShare.ReadWrite since Serilog holds its own write handle
        // open on the current day's file.
        private static async Task<List<LogEntryDto>> ReadTailEntriesAsync(string path, long windowBytes)
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var length = stream.Length;
            var start = Math.Max(0, length - windowBytes);
            stream.Seek(start, SeekOrigin.Begin);

            using var reader = new StreamReader(stream);
            var text = await reader.ReadToEndAsync();
            var lines = text.Split('\n');

            var entries = new List<LogEntryDto>();
            foreach (var rawLine in lines)
            {
                var line = rawLine.TrimEnd('\r');
                if (line.Length == 0) continue;

                var match = EntryStartRegex().Match(line);
                if (match.Success)
                {
                    var timestampText = line[..line.IndexOf('[')].Trim();
                    DateTime.TryParse(timestampText, out var timestamp);
                    entries.Add(new LogEntryDto
                    {
                        Timestamp = timestamp == default ? null : timestamp,
                        Level = match.Groups[1].Value,
                        Message = line[match.Length..],
                    });
                }
                else if (entries.Count > 0)
                {
                    // A continuation line (exception stack trace) belonging to the most recent entry.
                    entries[^1].Message += "\n" + line;
                }
                // else: a non-matching line before any entry was found in this window is a fragment of
                // an entry that started before our seek point (only possible when start > 0) - drop it
                // rather than show a truncated partial entry.
            }

            return entries;
        }
    }
}
