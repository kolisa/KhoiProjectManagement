using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace KhoiProjectManagement.Infrastructure.Data
{
    // Postgres' timestamptz columns require DateTime values to be UTC-kinded. Several DateTime
    // properties (e.g. Project.StartDate/EndDate, ProjectTask.DueDate) are set straight from API
    // request DTOs and arrive as DateTimeKind.Unspecified from JSON deserialization. Every DateTime
    // in this schema is either a server timestamp (already UtcNow) or a date-only business field
    // where time-of-day doesn't matter, so treating unspecified values as UTC (not converting from
    // local time) is the correct semantic here.
    public class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
    {
        public UtcDateTimeConverter() : base(
            v => v.Kind == DateTimeKind.Utc ? v : DateTime.SpecifyKind(v, DateTimeKind.Utc),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
        {
        }
    }
}
