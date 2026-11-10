using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using Microsoft.EntityFrameworkCore;

namespace KhoiProjectManagement.Application
{
    public interface IPmLabelService
    {
        // Labels already used by work items in the project, for the picker and the backlog filter.
        Task<List<LabelDto>> GetProjectLabelsAsync(PmCaller caller, int projectId);
        Task<List<LabelDto>> SetLabelsAsync(PmCaller caller, int itemId, SetLabelsDto dto);
    }

    // Labels reuse the app's existing Tag / TaskTag tables, so a label set here is the same tag the classic
    // Tasks tab shows. Tags are global, hence the picker only ever lists the ones already used in the project.
    public class PmLabelService : IPmLabelService
    {
        public const int MaxLabelsPerItem = 10;
        public const int MaxLabelLength = 30;
        // The tags every install starts with (see the seed data). They stay even when nothing uses them.
        private static readonly string[] SeededTagNames = { "web", "mobile", "design", "development", "urgent" };

        // Tag has no unique index on Name (the classic screens may already hold case-variant duplicates, so one
        // cannot be added safely), so find-or-create is serialised here instead: two people adding the same new
        // label at once would otherwise each create it. This relies on the API running as a single instance,
        // which is how it is deployed.
        private static readonly SemaphoreSlim LabelWrites = new(1, 1);

        private static readonly string[] Palette = { "#3B82F6", "#10B981", "#F59E0B", "#EF4444", "#8B5CF6", "#EC4899", "#14B8A6", "#6366F1" };

        private readonly IRepository<Tag> _tags;
        private readonly IRepository<TaskTag> _taskTags;
        private readonly IRepository<ProjectTask> _tasks;
        private readonly IProjectAccessService _access;
        private readonly IActivityLogService _activity;
        private readonly IUnitOfWork _uow;

        public PmLabelService(IRepository<Tag> tags, IRepository<TaskTag> taskTags, IRepository<ProjectTask> tasks,
            IProjectAccessService access, IActivityLogService activity, IUnitOfWork uow)
        {
            _tags = tags; _taskTags = taskTags; _tasks = tasks; _access = access; _activity = activity; _uow = uow;
        }

        // A stable colour per name, so the same label looks the same everywhere without anyone choosing one.
        public static string ColorFor(string name)
        {
            var hash = 17;
            foreach (var c in name.ToLowerInvariant()) hash = unchecked(hash * 31 + c);
            return Palette[Math.Abs(hash % Palette.Length)];
        }

        public sealed record ProjectLabelRow(int TagId, string Name, string Color);

        // Static query builders: their SQL translation is covered by tests that need no database.
        public static IQueryable<ProjectLabelRow> ProjectLabelsQuery(IQueryable<TaskTag> taskTags, int projectId) =>
            taskTags.AsNoTracking().Where(tt => tt.Task.ProjectId == projectId)
                .Select(tt => new ProjectLabelRow(tt.TagId, tt.Tag.Name, tt.Tag.Color)).Distinct();

        public static IQueryable<Tag> FindTagsQuery(IQueryable<Tag> tags, IReadOnlyCollection<string> lowerCaseNames) =>
            tags.Where(t => lowerCaseNames.Contains(t.Name.ToLower()));

        // Tags that nothing references any more (no task, project or wiki page).
        public static IQueryable<Tag> OrphanedTagsQuery(IQueryable<Tag> tags, IReadOnlyCollection<int> candidateIds) =>
            tags.Where(t => candidateIds.Contains(t.Id) && !t.TaskTags.Any() && !t.ProjectTags.Any() && !t.WikiPageTags.Any());

        public static IQueryable<TaskTag> CurrentLabelsQuery(IQueryable<TaskTag> taskTags, int itemId) =>
            taskTags.Where(tt => tt.TaskId == itemId).Include(tt => tt.Tag);

        public async Task<List<LabelDto>> GetProjectLabelsAsync(PmCaller caller, int projectId)
        {
            await _access.RequireAsync(caller, projectId, PmAction.View);
            var rows = await ProjectLabelsQuery(_taskTags.Query(), projectId).ToListAsync();
            return rows.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                .Select(r => new LabelDto { Id = r.TagId, Name = r.Name, Color = r.Color }).ToList();
        }

        public async Task<List<LabelDto>> SetLabelsAsync(PmCaller caller, int itemId, SetLabelsDto dto)
        {
            var item = await _tasks.Query().AsNoTracking().Where(t => t.Id == itemId)
                .Select(t => new { t.Id, t.ProjectId, t.Title }).FirstOrDefaultAsync() ?? throw new KeyNotFoundException();
            await _access.RequireAsync(caller, item.ProjectId, PmAction.EditItem);

            var names = dto.Labels
                .Select(l => string.Join(' ', (l ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries)))
                .Where(l => l.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (names.Count > MaxLabelsPerItem)
                throw new InvalidOperationException($"An item can have at most {MaxLabelsPerItem} labels.");
            if (names.Any(n => n.Length > MaxLabelLength))
                throw new InvalidOperationException($"A label can be at most {MaxLabelLength} characters.");

            await LabelWrites.WaitAsync();
            try
            {
                var lowered = names.Select(n => n.ToLower()).ToList();
                var known = await FindTagsQuery(_tags.Query(), lowered).ToListAsync();
                var byName = known.GroupBy(t => t.Name.ToLowerInvariant()).ToDictionary(g => g.Key, g => g.First());

                var current = await CurrentLabelsQuery(_taskTags.Query(), itemId).ToListAsync();
                var wanted = new List<Tag>();
                foreach (var name in names)
                {
                    if (!byName.TryGetValue(name.ToLowerInvariant(), out var tag))
                    {
                        tag = new Tag { Name = name, Color = ColorFor(name) };
                        byName[name.ToLowerInvariant()] = tag;
                    }
                    wanted.Add(tag);
                }

                var wantedKeys = wanted.Select(t => t.Name.ToLowerInvariant()).ToHashSet();
                var removed = current.Where(tt => !wantedKeys.Contains(tt.Tag.Name.ToLowerInvariant())).ToList();
                _taskTags.RemoveRange(removed);
                var currentKeys = current.Select(tt => tt.Tag.Name.ToLowerInvariant()).ToHashSet();
                foreach (var tag in wanted.Where(t => !currentKeys.Contains(t.Name.ToLowerInvariant())))
                    _taskTags.Add(new TaskTag { TaskId = itemId, Tag = tag });

                await _uow.SaveChangesAsync();

                // A label nobody uses any more is deleted, so the shared tag table does not fill with leftovers
                // (the built-in tags are never touched).
                if (removed.Count > 0)
                {
                    var orphans = (await OrphanedTagsQuery(_tags.Query(), removed.Select(tt => tt.TagId).Distinct().ToList()).ToListAsync())
                        .Where(t => !SeededTagNames.Contains(t.Name, StringComparer.OrdinalIgnoreCase)).ToList();
                    if (orphans.Count > 0)
                    {
                        _tags.RemoveRange(orphans);
                        await _uow.SaveChangesAsync();
                    }
                }

                await _activity.LogAsync("WorkItem", item.Id, item.Title, caller.UserId, "labelled", names.Count == 0 ? "none" : string.Join(", ", names));

                return wanted.Select(t => new LabelDto { Id = t.Id, Name = t.Name, Color = t.Color }).OrderBy(l => l.Name, StringComparer.OrdinalIgnoreCase).ToList();
            }
            finally
            {
                LabelWrites.Release();
            }
        }
    }
}
