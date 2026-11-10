using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using Microsoft.EntityFrameworkCore;

namespace KhoiProjectManagement.Application
{
    public interface IPmInsightsService
    {
        Task<ProjectOverviewDto> GetOverviewAsync(PmCaller caller, int projectId);
        Task<List<MyWorkItemDto>> GetMyWorkAsync(PmCaller caller);
        Task<List<ActivityEntryDto>> GetItemActivityAsync(PmCaller caller, int itemId);
    }

    // Turns logged actions into short readable sentences. Pure, so it is tested directly.
    public static class PmActivityText
    {
        public static string Humanize(string action) => action.Replace('-', ' ');

        // forItem: the row sits on that item's own timeline, so the sentence needs no item name.
        public static string Describe(string entityType, string action, string? details, string entityName, bool forItem)
        {
            if (entityType == "Project")
            {
                return action switch
                {
                    "member-removed" => $"removed {entityName} from the project",
                    "member-role-set" => $"set a project role: {entityName}",
                    "members-imported" => $"imported members: {details ?? entityName}",
                    "board-configured" => "changed the board columns",
                    _ => $"{Humanize(action)}: {entityName}",
                };
            }

            if (forItem)
            {
                return action switch
                {
                    "created" => "created this item",
                    "updated" => "edited this item",
                    "deleted" => "deleted this item",
                    "commented" => "commented",
                    "attached" => $"attached {details}",
                    "linked" => $"linked this item ({details})",
                    "moved" => $"moved this item ({details})",
                    "sprint-changed" => $"changed the sprint to {details}",
                    "labelled" => $"set labels: {details}",
                    _ => Humanize(action),
                };
            }

            var verb = action switch
            {
                "created" => "created",
                "updated" => "edited",
                "deleted" => "deleted",
                "commented" => "commented on",
                "attached" => $"attached {details} to",
                "linked" => $"linked ({details}) on",
                "moved" => $"moved ({details})",
                "sprint-changed" => $"changed the sprint to {details} for",
                "labelled" => $"set labels ({details}) on",
                _ => $"{Humanize(action)} on",
            };
            return $"{verb} \"{entityName}\"";
        }

        public static string StatusLabel(string status) => status switch
        {
            "todo" => "To do", "in-progress" => "In progress", "blocked" => "Blocked", "completed" => "Done", _ => status,
        };
    }

    // Phase 7: the read-side views that tie the module together - a per-project overview, a cross-project
    // "my work" list, and a per-item history. All read-only and derived from existing data.
    public class PmInsightsService : IPmInsightsService
    {
        private static readonly string[] Containers = { "Epic", "Feature" };
        private static readonly string[] PriorityOrder = { "high", "medium", "low" };

        private readonly IRepository<ProjectTask> _tasks;
        private readonly IRepository<Sprint> _sprints;
        private readonly IRepository<Project> _projects;
        private readonly IRepository<TaskStatusHistory> _history;
        private readonly IRepository<ActivityLogEntry> _log;
        private readonly IProjectAccessService _access;

        public PmInsightsService(
            IRepository<ProjectTask> tasks, IRepository<Sprint> sprints, IRepository<Project> projects,
            IRepository<TaskStatusHistory> history, IRepository<ActivityLogEntry> log, IProjectAccessService access)
        {
            _tasks = tasks; _sprints = sprints; _projects = projects; _history = history; _log = log; _access = access;
        }

        public sealed record Row(int Id, string Title, string Type, string Status, string Priority, DateTime DueDate,
            int? StoryPoints, int? SprintId, int? AssignedToId, string? AssigneeName, bool HasDue);

        public sealed record ActivityRow(string EntityType, int? EntityId, string EntityNameSnapshot, string ActorNameSnapshot, string Action, string? Details, DateTime Timestamp);
        public sealed record TransitionRow(string? FromStatus, string ToStatus, DateTime ChangedAt, string? Name);
        public sealed record MyWorkRow(int Id, int ProjectId, string ProjectName, string Title, string Type, string Status, string Priority,
            int? StoryPoints, DateTime DueDate, bool HasExplicitDueDate, string? SprintName);

        // The queries below are static builders so their translation to SQL is covered by tests that need no database.
        public static IQueryable<Row> OverviewRowsQuery(IQueryable<ProjectTask> tasks, int projectId) =>
            tasks.AsNoTracking().Where(t => t.ProjectId == projectId)
                .Select(t => new Row(t.Id, t.Title, t.Type, t.Status, t.Priority, t.DueDate, t.StoryPoints, t.SprintId, t.AssignedToId,
                    t.AssignedTo != null ? t.AssignedTo.Name : null, t.HasExplicitDueDate));

        public static IQueryable<ActivityRow> RecentActivityQuery(IQueryable<ActivityLogEntry> log, int projectId, IReadOnlyCollection<int> itemIds, int take) =>
            log.AsNoTracking()
                .Where(a => (a.EntityType == "WorkItem" && a.EntityId != null && itemIds.Contains(a.EntityId.Value))
                         || (a.EntityType == "Project" && a.EntityId == projectId))
                .OrderByDescending(a => a.Timestamp).Take(take)
                .Select(a => new ActivityRow(a.EntityType, a.EntityId, a.EntityNameSnapshot, a.ActorNameSnapshot, a.Action, a.Details, a.Timestamp));

        // "moved" log rows are skipped: every move is already a recorded status transition.
        public static IQueryable<ActivityRow> ItemLogQuery(IQueryable<ActivityLogEntry> log, int itemId) =>
            log.AsNoTracking().Where(a => a.EntityType == "WorkItem" && a.EntityId == itemId && a.Action != "moved")
                .Select(a => new ActivityRow(a.EntityType, a.EntityId, a.EntityNameSnapshot, a.ActorNameSnapshot, a.Action, a.Details, a.Timestamp));

        // Each item's first row (FromStatus null) is its creation, covered by the "created" log entry.
        public static IQueryable<TransitionRow> ItemTransitionsQuery(IQueryable<TaskStatusHistory> history, int itemId) =>
            history.AsNoTracking().Where(h => h.TaskId == itemId && h.FromStatus != null)
                .Select(h => new TransitionRow(h.FromStatus, h.ToStatus, h.ChangedAt, h.ChangedBy != null ? h.ChangedBy.Name : null));

        public static IQueryable<MyWorkRow> MyWorkQuery(IQueryable<ProjectTask> tasks, int userId) =>
            tasks.AsNoTracking()
                .Where(t => t.AssignedToId == userId && t.Status != "completed" && !Containers.Contains(t.Type))
                .Select(t => new MyWorkRow(t.Id, t.ProjectId, t.Project.Name, t.Title, t.Type, t.Status, t.Priority, t.StoryPoints,
                    t.DueDate, t.HasExplicitDueDate, t.Sprint != null ? t.Sprint.Name : null));

        private static int PriorityRank(string priority)
        {
            var i = Array.IndexOf(PriorityOrder, priority);
            return i < 0 ? PriorityOrder.Length : i;
        }

        public async Task<ProjectOverviewDto> GetOverviewAsync(PmCaller caller, int projectId)
        {
            await _access.RequireAsync(caller, projectId, PmAction.View);
            var now = DateTime.UtcNow;

            var projectName = await _projects.Query().AsNoTracking().Where(p => p.Id == projectId).Select(p => p.Name).FirstAsync();

            var all = await OverviewRowsQuery(_tasks.Query(), projectId).ToListAsync();
            var flow = all.Where(t => !Containers.Contains(t.Type)).ToList();
            var open = flow.Where(t => t.Status != "completed").ToList();

            var overview = new ProjectOverviewDto
            {
                ProjectId = projectId,
                ProjectName = projectName,
                StatusCounts = PmReportService.FlowStatuses.ToDictionary(s => s, s => flow.Count(t => t.Status == s)),
                OpenPoints = open.Sum(t => t.StoryPoints ?? 0),
                OverdueCount = open.Count(t => t.HasDue && t.DueDate < now),
                BlockedCount = open.Count(t => t.Status == "blocked"),
                UnassignedCount = open.Count(t => t.AssignedToId == null),
            };

            OverviewItemDto Map(Row t) => new()
            {
                Id = t.Id, Title = t.Title, Type = t.Type, Status = t.Status, Priority = t.Priority, DueDate = t.DueDate,
                HasDueDate = t.HasDue, IsOverdue = t.Status != "completed" && t.HasDue && t.DueDate < now, AssignedToName = t.AssigneeName,
            };

            overview.DueSoon = open.Where(t => t.HasDue && t.DueDate >= now && t.DueDate < now.AddDays(7))
                .OrderBy(t => t.DueDate).Take(6).Select(t => Map(t)).ToList();
            overview.MyOpenItems = open.Where(t => t.AssignedToId == caller.UserId)
                .OrderBy(t => t.HasDue && t.DueDate < now ? 0 : 1).ThenBy(t => PriorityRank(t.Priority)).ThenBy(t => t.HasDue ? 0 : 1).ThenBy(t => t.DueDate)
                .Take(8).Select(t => Map(t)).ToList();

            var sprint = await _sprints.Query().AsNoTracking().FirstOrDefaultAsync(s => s.ProjectId == projectId && s.State == SprintStates.Active);
            if (sprint != null)
            {
                var inSprint = flow.Where(t => t.SprintId == sprint.Id).ToList();
                overview.ActiveSprint = new OverviewSprintDto
                {
                    Id = sprint.Id, Name = sprint.Name, Goal = sprint.Goal, StartDate = sprint.StartDate, EndDate = sprint.EndDate,
                    DaysLeft = Math.Max(0, (sprint.EndDate.Date - now.Date).Days),
                    TotalPoints = inSprint.Sum(t => t.StoryPoints ?? 0),
                    DonePoints = inSprint.Where(t => t.Status == "completed").Sum(t => t.StoryPoints ?? 0),
                    ItemsTotal = inSprint.Count,
                    ItemsDone = inSprint.Count(t => t.Status == "completed"),
                };
            }

            var titles = all.ToDictionary(t => t.Id, t => t.Title);
            var ids = titles.Keys.ToList();
            var entries = await RecentActivityQuery(_log.Query(), projectId, ids, 10).ToListAsync();
            overview.RecentActivity = entries.Select(a => new ActivityEntryDto
            {
                When = a.Timestamp, ActorName = a.ActorNameSnapshot,
                Text = PmActivityText.Describe(a.EntityType, a.Action, a.Details, a.EntityNameSnapshot, forItem: false),
                ItemId = a.EntityType == "WorkItem" ? a.EntityId : null,
                ItemTitle = a.EntityType == "WorkItem" ? a.EntityNameSnapshot : null,
            }).ToList();

            return overview;
        }

        public async Task<List<MyWorkItemDto>> GetMyWorkAsync(PmCaller caller)
        {
            if (!await _access.HasModuleAccessAsync(caller))
                throw new ForbiddenException("You do not have access to the Project Management module.");

            var now = DateTime.UtcNow;
            var rows = await MyWorkQuery(_tasks.Query(), caller.UserId).ToListAsync();

            return rows
                .OrderBy(r => r.HasExplicitDueDate && r.DueDate < now ? 0 : 1).ThenBy(r => r.HasExplicitDueDate ? 0 : 1).ThenBy(r => r.DueDate).ThenBy(r => PriorityRank(r.Priority))
                .Take(200)
                .Select(r => new MyWorkItemDto
                {
                    Id = r.Id, ProjectId = r.ProjectId, ProjectName = r.ProjectName, Title = r.Title, Type = r.Type, Status = r.Status,
                    Priority = r.Priority, StoryPoints = r.StoryPoints, DueDate = r.DueDate, HasDueDate = r.HasExplicitDueDate, IsOverdue = r.HasExplicitDueDate && r.DueDate < now, SprintName = r.SprintName,
                }).ToList();
        }

        public async Task<List<ActivityEntryDto>> GetItemActivityAsync(PmCaller caller, int itemId)
        {
            var projectId = await _tasks.Query().AsNoTracking().Where(t => t.Id == itemId).Select(t => (int?)t.ProjectId).FirstOrDefaultAsync()
                ?? throw new KeyNotFoundException();
            await _access.RequireAsync(caller, projectId, PmAction.View);

            // "moved" log rows are skipped: every move is already a recorded status transition below.
            var logged = await ItemLogQuery(_log.Query(), itemId).ToListAsync();

            // Skip each item's first row (FromStatus null): that is its creation, covered by the "created" entry.
            var transitions = await ItemTransitionsQuery(_history.Query(), itemId).ToListAsync();

            return logged.Select(a => new ActivityEntryDto
                {
                    When = a.Timestamp, ActorName = a.ActorNameSnapshot, Kind = "activity",
                    Text = PmActivityText.Describe(a.EntityType, a.Action, a.Details, a.EntityNameSnapshot, forItem: true),
                })
                .Concat(transitions.Select(h => new ActivityEntryDto
                {
                    When = h.ChangedAt, ActorName = h.Name ?? "Someone", Kind = "status",
                    Text = $"moved this from {PmActivityText.StatusLabel(h.FromStatus!)} to {PmActivityText.StatusLabel(h.ToStatus)}",
                }))
                .OrderByDescending(e => e.When)
                .Take(100)
                .ToList();
        }
    }
}
