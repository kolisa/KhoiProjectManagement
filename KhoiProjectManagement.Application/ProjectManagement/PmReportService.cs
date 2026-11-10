using System.Globalization;
using System.Text;
using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using Microsoft.EntityFrameworkCore;

namespace KhoiProjectManagement.Application
{
    public interface IPmReportService
    {
        Task<CumulativeFlowDto> GetCumulativeFlowAsync(PmCaller caller, int projectId, int days);
        Task<FlowMetricsDto> GetFlowMetricsAsync(PmCaller caller, int projectId, int days);
        Task<List<WorkloadRowDto>> GetWorkloadAsync(PmCaller caller, int projectId);
        Task<CsvExportDto> ExportWorkItemsCsvAsync(PmCaller caller, int projectId);
    }

    // Pure calculations, kept separate from the data access so they can be tested directly.
    public static class PmReportMath
    {
        // Nearest-rank percentile of an ascending-sorted list; null when empty.
        public static double? Percentile(IReadOnlyList<double> sorted, double percentile)
        {
            if (sorted.Count == 0) return null;
            var rank = (int)Math.Ceiling(percentile / 100.0 * sorted.Count);
            return sorted[Math.Clamp(rank, 1, sorted.Count) - 1];
        }

        public static TimeStatsDto Stats(IEnumerable<double> days)
        {
            var sorted = days.OrderBy(d => d).ToList();
            if (sorted.Count == 0) return new TimeStatsDto();
            return new TimeStatsDto
            {
                Samples = sorted.Count,
                Average = Math.Round(sorted.Average(), 1),
                Median = Math.Round(Percentile(sorted, 50)!.Value, 1),
                P85 = Math.Round(Percentile(sorted, 85)!.Value, 1),
            };
        }

        // Monday of the week containing the date.
        public static DateTime WeekStart(DateTime date)
        {
            var d = date.Date;
            return d.AddDays(-(((int)d.DayOfWeek + 6) % 7));
        }

        // RFC 4180 quoting, plus a leading apostrophe on cells a spreadsheet would execute as a formula
        // (user-entered titles flow into this file, and it is meant to be opened in Excel).
        public static string CsvCell(string? value)
        {
            var text = value ?? string.Empty;
            if (text.Length > 0 && "=+-@\t\r".Contains(text[0])) text = "'" + text;
            return text.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0 ? "\"" + text.Replace("\"", "\"\"") + "\"" : text;
        }
    }

    // Phase 5 of the PM module: read-only analytics derived from ProjectTask and the append-only
    // TaskStatusHistory log. Epics/Features are containers, not flowing work, so (like the board) they
    // are excluded from every measure.
    public class PmReportService : IPmReportService
    {
        // Order items stack on the cumulative-flow chart, bottom first.
        public static readonly string[] FlowStatuses = { "completed", "in-progress", "blocked", "todo" };
        private static readonly string[] Containers = { "Epic", "Feature" };

        private readonly IRepository<ProjectTask> _tasks;
        private readonly IRepository<TaskStatusHistory> _history;
        private readonly IProjectAccessService _access;

        public PmReportService(IRepository<ProjectTask> tasks, IRepository<TaskStatusHistory> history, IProjectAccessService access)
        {
            _tasks = tasks;
            _history = history;
            _access = access;
        }

        public sealed record TransitionRow(int TaskId, string? FromStatus, string ToStatus, DateTime ChangedAt);
        public sealed record WorkloadRow(int? AssignedToId, string? Name, string Status, int? StoryPoints, DateTime DueDate, bool HasExplicitDueDate);

        // Static query builders: their SQL translation is covered by tests that need no database.
        public static IQueryable<TransitionRow> TransitionsQuery(IQueryable<TaskStatusHistory> history, IReadOnlyCollection<int> taskIds, DateTime from) =>
            history.AsNoTracking().Where(h => taskIds.Contains(h.TaskId) && h.ChangedAt >= from)
                .Select(h => new TransitionRow(h.TaskId, h.FromStatus, h.ToStatus, h.ChangedAt));

        public static IQueryable<WorkloadRow> WorkloadQuery(IQueryable<ProjectTask> flowItems, DateTime recent) =>
            flowItems.Where(t => t.Status != "completed" || (t.CompletedAt != null && t.CompletedAt >= recent))
                .Select(t => new WorkloadRow(t.AssignedToId, t.AssignedTo != null ? t.AssignedTo.Name : null, t.Status, t.StoryPoints, t.DueDate, t.HasExplicitDueDate));

        private IQueryable<ProjectTask> FlowItems(int projectId) =>
            _tasks.Query().AsNoTracking().Where(t => t.ProjectId == projectId && !Containers.Contains(t.Type));

        public async Task<CumulativeFlowDto> GetCumulativeFlowAsync(PmCaller caller, int projectId, int days)
        {
            await _access.RequireAsync(caller, projectId, PmAction.View);
            days = Math.Clamp(days, 7, 180);

            var to = DateTime.UtcNow.Date;
            var from = to.AddDays(-(days - 1));

            var items = await FlowItems(projectId).Select(t => new { t.Id, t.Status, t.CreatedAt }).ToListAsync();
            var ids = items.Select(i => i.Id).ToList();

            // Only the transitions inside the window are loaded - not the project's whole history. What an item
            // looked like before the window is recovered from the first in-window transition's FromStatus, and
            // an item with no transitions in the window has simply kept its current status throughout.
            var transitions = await TransitionsQuery(_history.Query(), ids, from).ToListAsync();
            var byTask = transitions.GroupBy(h => h.TaskId)
                .ToDictionary(g => g.Key, g => g.OrderBy(h => h.ChangedAt).ToList());

            var result = new CumulativeFlowDto { From = from, To = to, Statuses = FlowStatuses.ToList() };

            for (var day = from; day <= to; day = day.AddDays(1))
            {
                var cutoff = day.AddDays(1);
                var counts = FlowStatuses.ToDictionary(s => s, _ => 0);
                foreach (var item in items)
                {
                    string? status;
                    if (!byTask.TryGetValue(item.Id, out var rows))
                    {
                        status = item.CreatedAt < cutoff ? item.Status : null;
                    }
                    else
                    {
                        var last = rows.LastOrDefault(r => r.ChangedAt < cutoff);
                        // Before its first in-window transition an item is in that transition's FromStatus -
                        // or did not exist yet when the transition is its creation (FromStatus null).
                        status = last != null ? last.ToStatus : rows[0].FromStatus;
                    }
                    if (status != null && counts.ContainsKey(status)) counts[status]++;
                }
                result.Points.Add(new CumulativeFlowPointDto { Date = day, Counts = counts });
            }
            return result;
        }

        public async Task<FlowMetricsDto> GetFlowMetricsAsync(PmCaller caller, int projectId, int days)
        {
            await _access.RequireAsync(caller, projectId, PmAction.View);
            days = Math.Clamp(days, 7, 365);
            var since = DateTime.UtcNow.Date.AddDays(-(days - 1));

            var done = await FlowItems(projectId)
                .Where(t => t.Status == "completed" && t.CompletedAt != null && t.CompletedAt >= since)
                .Select(t => new { t.CreatedAt, t.StartedAt, CompletedAt = t.CompletedAt!.Value, t.StoryPoints })
                .ToListAsync();

            var lead = done.Select(t => Math.Max(0, (t.CompletedAt - t.CreatedAt).TotalDays));
            var cycle = done.Where(t => t.StartedAt != null).Select(t => Math.Max(0, (t.CompletedAt - t.StartedAt!.Value).TotalDays));

            var weeks = new List<ThroughputWeekDto>();
            var thisWeek = PmReportMath.WeekStart(DateTime.UtcNow);
            for (var w = PmReportMath.WeekStart(since); w <= thisWeek; w = w.AddDays(7))
            {
                var inWeek = done.Where(t => t.CompletedAt >= w && t.CompletedAt < w.AddDays(7)).ToList();
                weeks.Add(new ThroughputWeekDto { WeekStart = w, Items = inWeek.Count, Points = inWeek.Sum(t => t.StoryPoints ?? 0) });
            }

            return new FlowMetricsDto
            {
                Days = days,
                CompletedCount = done.Count,
                CompletedPoints = done.Sum(t => t.StoryPoints ?? 0),
                LeadTime = PmReportMath.Stats(lead),
                CycleTime = PmReportMath.Stats(cycle),
                Weeks = weeks,
            };
        }

        public async Task<List<WorkloadRowDto>> GetWorkloadAsync(PmCaller caller, int projectId)
        {
            await _access.RequireAsync(caller, projectId, PmAction.View);
            var now = DateTime.UtcNow;
            var recent = now.AddDays(-30);

            var rows = await WorkloadQuery(FlowItems(projectId), recent).ToListAsync();

            return rows.GroupBy(r => r.AssignedToId)
                .Select(g =>
                {
                    var open = g.Where(r => r.Status != "completed").ToList();
                    return new WorkloadRowDto
                    {
                        UserId = g.Key,
                        Name = g.Select(r => r.Name).FirstOrDefault(n => n != null) ?? "Unassigned",
                        OpenItems = open.Count,
                        OpenPoints = open.Sum(r => r.StoryPoints ?? 0),
                        InProgress = open.Count(r => r.Status == "in-progress"),
                        Blocked = open.Count(r => r.Status == "blocked"),
                        Overdue = open.Count(r => r.HasExplicitDueDate && r.DueDate < now),
                        DoneLast30Days = g.Count(r => r.Status == "completed"),
                    };
                })
                .OrderByDescending(r => r.OpenPoints).ThenByDescending(r => r.OpenItems).ThenBy(r => r.Name)
                .ToList();
        }

        public async Task<CsvExportDto> ExportWorkItemsCsvAsync(PmCaller caller, int projectId)
        {
            await _access.RequireAsync(caller, projectId, PmAction.View);

            var items = await _tasks.Query().AsNoTracking().Where(t => t.ProjectId == projectId)
                .OrderBy(t => t.Rank).ThenBy(t => t.Id)
                .Select(t => new
                {
                    t.Id, t.Type, t.Title, t.Status, t.Priority, t.StoryPoints,
                    Assignee = t.AssignedTo != null ? t.AssignedTo.Name : null,
                    Sprint = t.Sprint != null ? t.Sprint.Name : null,
                    Parent = t.Parent != null ? t.Parent.Title : null,
                    t.CreatedAt, t.StartedAt, t.CompletedAt, t.DueDate,
                }).ToListAsync();

            string Date(DateTime? d) => d?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;

            var csv = new StringBuilder();
            csv.Append("Id,Type,Title,Status,Priority,Story points,Assignee,Sprint,Parent,Created,Started,Completed,Due\r\n");
            foreach (var i in items)
            {
                csv.Append(string.Join(',', new[]
                {
                    i.Id.ToString(CultureInfo.InvariantCulture), PmReportMath.CsvCell(i.Type), PmReportMath.CsvCell(i.Title),
                    PmReportMath.CsvCell(i.Status), PmReportMath.CsvCell(i.Priority), i.StoryPoints?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    PmReportMath.CsvCell(i.Assignee), PmReportMath.CsvCell(i.Sprint), PmReportMath.CsvCell(i.Parent),
                    Date(i.CreatedAt), Date(i.StartedAt), Date(i.CompletedAt), Date(i.DueDate),
                })).Append("\r\n");
            }

            // UTF-8 with a BOM so Excel reads non-ASCII names correctly.
            var content = new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
            return new CsvExportDto { FileName = $"project-{projectId}-work-items-{DateTime.UtcNow:yyyyMMdd}.csv", Content = content };
        }
    }
}
