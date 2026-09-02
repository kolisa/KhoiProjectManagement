using System.Text;

namespace KhoiProjectManagement.Application
{
    // Hand-rolled iCalendar (RFC 5545) text builder - deliberately not a NuGet dependency (Ical.Net
    // et al.) since the surface needed here is small: a handful of all-day VEVENTs plus yearly-
    // recurring birthdays, with no timezone/attendee/alarm complexity to justify a library.
    public static class IcsFeedBuilder
    {
        public record IcsEvent(string Uid, DateTime Date, string Summary, string? Description);
        public record IcsBirthday(string Uid, DateTime NextOccurrence, string Summary);

        public static string Build(IEnumerable<IcsEvent> events, IEnumerable<IcsBirthday> birthdays)
        {
            var sb = new StringBuilder();
            AppendLine(sb, "BEGIN:VCALENDAR");
            AppendLine(sb, "VERSION:2.0");
            AppendLine(sb, "PRODID:-//KhoiHub//Calendar//EN");
            AppendLine(sb, "CALSCALE:GREGORIAN");

            var dtstamp = DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ");

            foreach (var e in events)
            {
                AppendLine(sb, "BEGIN:VEVENT");
                AppendLine(sb, $"UID:{e.Uid}@khoipro");
                AppendLine(sb, $"DTSTAMP:{dtstamp}");
                AppendLine(sb, $"DTSTART;VALUE=DATE:{e.Date:yyyyMMdd}");
                AppendLine(sb, $"SUMMARY:{Escape(e.Summary)}");
                if (!string.IsNullOrWhiteSpace(e.Description))
                    AppendLine(sb, $"DESCRIPTION:{Escape(e.Description)}");
                AppendLine(sb, "END:VEVENT");
            }

            foreach (var b in birthdays)
            {
                // DTSTART uses this year's (or next occurrence's) date, never the actual birth year -
                // RRULE:FREQ=YEARLY makes it repeat every year from month/day alone, so no birth year
                // is ever exposed here, matching User.DateOfBirth's privacy rule for the Calendar feed.
                AppendLine(sb, "BEGIN:VEVENT");
                AppendLine(sb, $"UID:{b.Uid}@khoipro");
                AppendLine(sb, $"DTSTAMP:{dtstamp}");
                AppendLine(sb, $"DTSTART;VALUE=DATE:{b.NextOccurrence:yyyyMMdd}");
                AppendLine(sb, "RRULE:FREQ=YEARLY");
                AppendLine(sb, $"SUMMARY:{Escape(b.Summary)}");
                AppendLine(sb, "END:VEVENT");
            }

            AppendLine(sb, "END:VCALENDAR");
            return sb.ToString();
        }

        // RFC 5545 requires \, ;, and literal newlines escaped in text values, and CRLF line endings
        // with folding past 75 octets - most real-world calendar clients tolerate unfolded long lines,
        // but folding costs little and keeps this a strictly conforming feed.
        private static string Escape(string value) => value
            .Replace("\\", "\\\\")
            .Replace(";", "\\;")
            .Replace(",", "\\,")
            .Replace("\r\n", "\\n")
            .Replace("\n", "\\n");

        private static void AppendLine(StringBuilder sb, string line)
        {
            const int maxOctets = 75;
            if (Encoding.UTF8.GetByteCount(line) <= maxOctets)
            {
                sb.Append(line).Append("\r\n");
                return;
            }

            var remaining = line;
            var first = true;
            while (remaining.Length > 0)
            {
                var take = Math.Min(remaining.Length, first ? maxOctets : maxOctets - 1);
                // Trim back until the chunk's UTF-8 byte length fits, so a multi-byte char is never split.
                while (Encoding.UTF8.GetByteCount(remaining[..take]) > (first ? maxOctets : maxOctets - 1) && take > 1)
                    take--;

                sb.Append(first ? "" : " ").Append(remaining[..take]).Append("\r\n");
                remaining = remaining[take..];
                first = false;
            }
        }
    }
}
