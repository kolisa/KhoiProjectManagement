using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using Microsoft.EntityFrameworkCore;

namespace KhoiProjectManagement.Application
{
    public interface IPmEffortService
    {
        Task<WorkItemEffortDto> GetItemEffortAsync(PmCaller caller, int itemId);
        Task<WorkItemEffortDto> SetItemEffortAsync(PmCaller caller, int itemId, SaveWorkItemEffortDto dto);
        // Points per person for the sprint, drillable person > item > category.
        Task<SprintEffortDto> GetSprintEffortAsync(PmCaller caller, int sprintId);
    }

    // Effort (points) is allocated to PEOPLE, per work item and per category, and a sprint's points are those allocations
    // summed per person - not one number for the whole sprint or the whole item.
    public class PmEffortService : IPmEffortService
    {
        public const decimal MaxPoints = 1000m;

        private readonly IRepository<WorkItemEffort> _efforts;
        private readonly IRepository<ProjectTask> _tasks;
        private readonly IRepository<Sprint> _sprints;
        private readonly IRepository<ProjectUser> _members;
        private readonly IProjectAccessService _access;
        private readonly IActivityLogService _activity;
        private readonly IUnitOfWork _uow;

        public PmEffortService(IRepository<WorkItemEffort> efforts, IRepository<ProjectTask> tasks, IRepository<Sprint> sprints,
            IRepository<ProjectUser> members, IProjectAccessService access, IActivityLogService activity, IUnitOfWork uow)
        {
            _efforts = efforts; _tasks = tasks; _sprints = sprints; _members = members; _access = access; _activity = activity; _uow = uow;
        }

        public sealed record EffortRow(int TaskId, int UserId, string UserName, string Category, decimal? Points);
        public sealed record SprintItemRow(int Id, string Title, string Type, string Status, int? ParentId, string? ParentTitle);
        public sealed record ItemRef(int ProjectId, string Title);

        // Static query builders: their SQL translation is covered by tests that need no database.
        public static IQueryable<EffortRow> ItemEffortQuery(IQueryable<WorkItemEffort> efforts, int itemId) =>
            efforts.AsNoTracking().Where(e => e.TaskId == itemId)
                .Select(e => new EffortRow(e.TaskId, e.UserId, e.User.Name, e.Category, e.Points));

        public static IQueryable<EffortRow> SprintEffortQuery(IQueryable<WorkItemEffort> efforts, int sprintId) =>
            efforts.AsNoTracking().Where(e => e.Task.SprintId == sprintId)
                .Select(e => new EffortRow(e.TaskId, e.UserId, e.User.Name, e.Category, e.Points));

        public static IQueryable<SprintItemRow> SprintItemsQuery(IQueryable<ProjectTask> tasks, int sprintId) =>
            tasks.AsNoTracking().Where(t => t.SprintId == sprintId)
                .Select(t => new SprintItemRow(t.Id, t.Title, t.Type, t.Status, t.ParentId, t.Parent != null ? t.Parent.Title : null));

        private static int Order(string category) { var i = Array.IndexOf(EffortCategories.All, category); return i < 0 ? int.MaxValue : i; }

        private static List<EffortCellDto> SumCells(IEnumerable<EffortRow> rows) =>
            rows.GroupBy(r => r.Category).OrderBy(g => Order(g.Key))
                .Select(g => new EffortCellDto { Category = g.Key, Points = g.All(r => r.Points == null) ? null : g.Sum(r => r.Points ?? 0) }).ToList();

        private async Task<ItemRef> ItemAsync(int itemId) =>
            await _tasks.Query().AsNoTracking().Where(t => t.Id == itemId).Select(t => new ItemRef(t.ProjectId, t.Title)).FirstOrDefaultAsync()
                ?? throw new KeyNotFoundException();

        public async Task<WorkItemEffortDto> GetItemEffortAsync(PmCaller caller, int itemId)
        {
            var item = await ItemAsync(itemId);
            await _access.RequireAsync(caller, item.ProjectId, PmAction.View);
            return Map(itemId, await ItemEffortQuery(_efforts.Query(), itemId).ToListAsync());
        }

        private static WorkItemEffortDto Map(int itemId, List<EffortRow> rows)
        {
            var people = rows.GroupBy(r => r.UserId).Select(g => new PersonEffortDto
            {
                UserId = g.Key,
                UserName = g.First().UserName,
                Cells = g.OrderBy(r => Order(r.Category)).Select(r => new EffortCellDto { Category = r.Category, Points = r.Points }).ToList(),
                Total = g.Sum(r => r.Points ?? 0),
            }).OrderBy(p => p.UserName).ToList();
            return new WorkItemEffortDto { ItemId = itemId, People = people, Total = people.Sum(p => p.Total) };
        }

        public async Task<WorkItemEffortDto> SetItemEffortAsync(PmCaller caller, int itemId, SaveWorkItemEffortDto dto)
        {
            var item = await ItemAsync(itemId);
            await _access.RequireAsync(caller, item.ProjectId, PmAction.EditItem);

            var people = dto.People.Where(p => p.Cells.Count > 0).ToList();
            if (people.GroupBy(p => p.UserId).Any(g => g.Count() > 1))
                throw new InvalidOperationException("Each person can appear only once.");
            foreach (var cell in people.SelectMany(p => p.Cells))
            {
                if (!EffortCategories.All.Contains(cell.Category)) throw new InvalidOperationException($"Unknown effort category '{cell.Category}'.");
                if (cell.Points is < 0 or > MaxPoints) throw new InvalidOperationException($"Points must be between 0 and {MaxPoints}.");
            }
            if (people.Any(p => p.Cells.GroupBy(c => c.Category).Any(g => g.Count() > 1)))
                throw new InvalidOperationException("A category can be given only once per person.");

            // Effort can only be allocated to someone who is on the project.
            var ids = people.Select(p => p.UserId).Distinct().ToList();
            var onProject = await _members.Query().AsNoTracking().Where(m => m.ProjectId == item.ProjectId && ids.Contains(m.UserId)).Select(m => m.UserId).ToListAsync();
            var existing = await _efforts.Query().Where(e => e.TaskId == itemId).ToListAsync();
            // Someone already on this item's effort who has since left the project may be kept (or removed) but not newly added.
            var alreadyOnItem = existing.Select(e => e.UserId).ToHashSet();
            if (ids.Except(onProject).Except(alreadyOnItem).Any()) throw new InvalidOperationException("Effort can only be allocated to people on this project.");

            _efforts.RemoveRange(existing);
            foreach (var p in people)
                foreach (var c in p.Cells)
                    _efforts.Add(new WorkItemEffort { TaskId = itemId, UserId = p.UserId, Category = c.Category, Points = c.Points is null ? null : Math.Round(c.Points.Value, 2) });
            await _uow.SaveChangesAsync();

            await _activity.LogAsync("WorkItem", itemId, item.Title, caller.UserId, "updated", "effort allocation");
            return await GetItemEffortAsync(caller, itemId);
        }

        public async Task<SprintEffortDto> GetSprintEffortAsync(PmCaller caller, int sprintId)
        {
            var sprint = await _sprints.Query().AsNoTracking().Where(s => s.Id == sprintId).Select(s => new { s.ProjectId, s.Name }).FirstOrDefaultAsync()
                ?? throw new KeyNotFoundException();
            await _access.RequireAsync(caller, sprint.ProjectId, PmAction.View);

            var items = (await SprintItemsQuery(_tasks.Query(), sprintId).ToListAsync()).ToDictionary(i => i.Id);
            var rows = await SprintEffortQuery(_efforts.Query(), sprintId).ToListAsync();

            SprintItemEffortDto ItemDto(SprintItemRow i, List<EffortRow> own) => new()
            {
                ItemId = i.Id, Title = i.Title, Type = i.Type, Status = i.Status, ParentId = i.ParentId, ParentTitle = i.ParentTitle,
                Cells = SumCells(own), Total = own.Sum(r => r.Points ?? 0),
            };

            var people = rows.GroupBy(r => r.UserId).Select(g =>
            {
                var itemDtos = g.GroupBy(r => r.TaskId).Where(t => items.ContainsKey(t.Key))
                    .Select(t => ItemDto(items[t.Key], t.ToList())).OrderByDescending(i => i.Total).ThenBy(i => i.Title).ToList();
                return new SprintPersonEffortDto
                {
                    UserId = g.Key, UserName = g.First().UserName, Items = itemDtos,
                    Categories = SumCells(g), Total = itemDtos.Sum(i => i.Total),
                    Done = itemDtos.Where(i => i.Status == "completed").Sum(i => i.Total),
                };
            }).OrderByDescending(p => p.Total).ThenBy(p => p.UserName).ToList();

            var allocated = rows.Select(r => r.TaskId).ToHashSet();
            return new SprintEffortDto
            {
                SprintId = sprintId, Name = sprint.Name, People = people,
                Total = people.Sum(p => p.Total), Done = people.Sum(p => p.Done),
                Unallocated = items.Values.Where(i => !allocated.Contains(i.Id)).OrderBy(i => i.Title).Select(i => ItemDto(i, new List<EffortRow>())).ToList(),
            };
        }
    }
}
