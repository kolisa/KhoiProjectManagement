using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using Microsoft.EntityFrameworkCore;

namespace KhoiProjectManagement.Application
{
    public interface IPmSprintService
    {
        Task<List<SprintDto>> GetSprintsAsync(PmCaller caller, int projectId);
        Task<SprintDto> CreateAsync(PmCaller caller, int projectId, SaveSprintDto dto);
        Task<SprintDto> UpdateAsync(PmCaller caller, int sprintId, SaveSprintDto dto);
        Task DeleteAsync(PmCaller caller, int sprintId);
        Task<SprintDto> StartAsync(PmCaller caller, int sprintId);
        Task<SprintDto> CompleteAsync(PmCaller caller, int sprintId, CompleteSprintDto dto);
        Task<WorkItemDto> AssignAsync(PmCaller caller, int itemId, int? sprintId);
        Task<BurndownDto> GetBurndownAsync(PmCaller caller, int sprintId);
        Task<VelocityDto> GetVelocityAsync(PmCaller caller, int projectId, int count);
    }

    // Scrum layer over the shared work items: a sprint is just a SprintId on ProjectTask plus lifecycle
    // (Planned -> Active -> Completed) and point snapshots. One active sprint per project.
    public class PmSprintService : IPmSprintService
    {
        // Containers (Epic/Feature) are planned through their child stories/tasks, not sprinted themselves.
        private static readonly string[] NonSprintTypes = { "Epic", "Feature" };

        private readonly IRepository<Sprint> _sprints;
        private readonly IRepository<ProjectTask> _tasks;
        private readonly IRepository<TaskStatusHistory> _history;
        private readonly IPmWorkItemService _workItems;
        private readonly IProjectAccessService _access;
        private readonly IActivityLogService _activity;
        private readonly IUnitOfWork _unitOfWork;

        public PmSprintService(
            IRepository<Sprint> sprints,
            IRepository<ProjectTask> tasks,
            IRepository<TaskStatusHistory> history,
            IPmWorkItemService workItems,
            IProjectAccessService access,
            IActivityLogService activity,
            IUnitOfWork unitOfWork)
        {
            _sprints = sprints;
            _tasks = tasks;
            _history = history;
            _workItems = workItems;
            _access = access;
            _activity = activity;
            _unitOfWork = unitOfWork;
        }

        public async Task<List<SprintDto>> GetSprintsAsync(PmCaller caller, int projectId)
        {
            await _access.RequireAsync(caller, projectId, PmAction.View);
            return await ProjectDto(_sprints.Query().AsNoTracking().Where(s => s.ProjectId == projectId))
                .OrderBy(s => s.State == SprintStates.Active ? 0 : s.State == SprintStates.Planned ? 1 : 2)
                .ThenBy(s => s.StartDate)
                .ToListAsync();
        }

        public async Task<SprintDto> CreateAsync(PmCaller caller, int projectId, SaveSprintDto dto)
        {
            await _access.RequireAsync(caller, projectId, PmAction.ManageSprint);

            var sprint = new Sprint
            {
                ProjectId = projectId,
                Name = dto.Name.Trim(),
                Goal = dto.Goal,
                StartDate = dto.StartDate.Date,
                EndDate = dto.EndDate.Date,
                Capacity = dto.Capacity,
                State = SprintStates.Planned,
            };
            _sprints.Add(sprint);
            await _unitOfWork.SaveChangesAsync();
            await _activity.LogAsync("Sprint", sprint.Id, sprint.Name, caller.UserId, "created");
            return await GetDtoAsync(sprint.Id);
        }

        public async Task<SprintDto> UpdateAsync(PmCaller caller, int sprintId, SaveSprintDto dto)
        {
            var sprint = await LoadAsync(sprintId);
            await _access.RequireAsync(caller, sprint.ProjectId, PmAction.ManageSprint);
            if (sprint.State == SprintStates.Completed)
                throw new InvalidOperationException("A completed sprint can no longer be edited.");

            sprint.Name = dto.Name.Trim();
            sprint.Goal = dto.Goal;
            sprint.StartDate = dto.StartDate.Date;
            sprint.EndDate = dto.EndDate.Date;
            sprint.Capacity = dto.Capacity;
            await _unitOfWork.SaveChangesAsync();
            await _activity.LogAsync("Sprint", sprint.Id, sprint.Name, caller.UserId, "updated");
            return await GetDtoAsync(sprint.Id);
        }

        public async Task DeleteAsync(PmCaller caller, int sprintId)
        {
            var sprint = await LoadAsync(sprintId);
            await _access.RequireAsync(caller, sprint.ProjectId, PmAction.ManageSprint);
            if (sprint.State != SprintStates.Planned)
                throw new InvalidOperationException("Only a planned sprint can be deleted. Complete an active sprint instead.");

            // Its items fall back to the backlog (FK is SetNull, but make it explicit for tracked entities).
            var items = await _tasks.Query().Where(t => t.SprintId == sprintId).ToListAsync();
            foreach (var item in items) item.SprintId = null;

            _sprints.Remove(sprint);
            await _unitOfWork.SaveChangesAsync();
            await _activity.LogAsync("Sprint", null, sprint.Name, caller.UserId, "deleted");
        }

        public async Task<SprintDto> StartAsync(PmCaller caller, int sprintId)
        {
            var sprint = await LoadAsync(sprintId);
            await _access.RequireAsync(caller, sprint.ProjectId, PmAction.ManageSprint);
            if (sprint.State != SprintStates.Planned)
                throw new InvalidOperationException("Only a planned sprint can be started.");
            if (await _sprints.Query().AnyAsync(s => s.ProjectId == sprint.ProjectId && s.State == SprintStates.Active))
                throw new InvalidOperationException("This project already has an active sprint. Complete it before starting another.");

            var points = await _tasks.Query().Where(t => t.SprintId == sprintId).SumAsync(t => t.StoryPoints ?? 0);
            sprint.State = SprintStates.Active;
            sprint.StartedAt = DateTime.UtcNow;
            sprint.CommittedPoints = points;

            await _unitOfWork.SaveChangesAsync();
            await _activity.LogAsync("Sprint", sprint.Id, sprint.Name, caller.UserId, "started", $"{points} points committed");
            return await GetDtoAsync(sprint.Id);
        }

        public async Task<SprintDto> CompleteAsync(PmCaller caller, int sprintId, CompleteSprintDto dto)
        {
            var sprint = await LoadAsync(sprintId);
            await _access.RequireAsync(caller, sprint.ProjectId, PmAction.ManageSprint);
            if (sprint.State != SprintStates.Active)
                throw new InvalidOperationException("Only an active sprint can be completed.");

            if (dto.MoveIncompleteToSprintId.HasValue)
            {
                var target = await _sprints.Query().AsNoTracking()
                    .Where(s => s.Id == dto.MoveIncompleteToSprintId && s.ProjectId == sprint.ProjectId && s.State == SprintStates.Planned)
                    .Select(s => s.Id).FirstOrDefaultAsync();
                if (target == 0)
                    throw new InvalidOperationException("Unfinished items can only move to a planned sprint in this project.");
            }

            var items = await _tasks.Query().Where(t => t.SprintId == sprintId).ToListAsync();
            var unfinished = items.Where(t => t.Status != "completed").ToList();
            foreach (var item in unfinished) item.SprintId = dto.MoveIncompleteToSprintId;

            sprint.State = SprintStates.Completed;
            sprint.CompletedAt = DateTime.UtcNow;
            sprint.CompletedPoints = items.Where(t => t.Status == "completed").Sum(t => t.StoryPoints ?? 0);

            await _unitOfWork.SaveChangesAsync();
            await _activity.LogAsync("Sprint", sprint.Id, sprint.Name, caller.UserId, "completed",
                $"{sprint.CompletedPoints} of {sprint.CommittedPoints ?? 0} points; {unfinished.Count} unfinished");
            return await GetDtoAsync(sprint.Id);
        }

        public async Task<WorkItemDto> AssignAsync(PmCaller caller, int itemId, int? sprintId)
        {
            var item = await _tasks.Query().Include(t => t.Sprint).FirstOrDefaultAsync(t => t.Id == itemId) ?? throw new KeyNotFoundException();
            await _access.RequireAsync(caller, item.ProjectId, PmAction.ManageSprint);

            if (NonSprintTypes.Contains(item.Type))
                throw new InvalidOperationException($"{item.Type} items are planned through their stories and tasks, not added to a sprint.");
            if (item.Sprint?.State == SprintStates.Completed)
                throw new InvalidOperationException("This item belongs to a completed sprint and can no longer be moved.");

            string? targetName = null;
            if (sprintId.HasValue)
            {
                var target = await _sprints.Query().AsNoTracking()
                    .Where(s => s.Id == sprintId && s.ProjectId == item.ProjectId)
                    .Select(s => new { s.State, s.Name }).FirstOrDefaultAsync()
                    ?? throw new InvalidOperationException("That sprint does not exist in this project.");
                if (target.State == SprintStates.Completed)
                    throw new InvalidOperationException("Items cannot be added to a completed sprint.");
                targetName = target.Name;
            }

            var previousSprintId = item.SprintId;
            item.SprintId = sprintId;
            item.UpdatedAt = DateTime.UtcNow;
            // Moving an item moves its subtasks with it (the ones that were in the same sprint, or in none), so a person's
            // effort on a subtask is counted in the sprint the parent is planned in.
            var subtasks = await _tasks.Query().Where(t => t.ParentId == itemId && t.Type == "Subtask" && t.Status != "completed"
                && (t.SprintId == previousSprintId || t.SprintId == null)).ToListAsync();
            foreach (var sub in subtasks) { sub.SprintId = sprintId; sub.UpdatedAt = DateTime.UtcNow; }
            await _unitOfWork.SaveChangesAsync();
            await _activity.LogAsync("WorkItem", item.Id, item.Title, caller.UserId, "sprint-changed", targetName ?? "backlog");
            return await _workItems.GetAsync(caller, itemId);
        }

        public async Task<BurndownDto> GetBurndownAsync(PmCaller caller, int sprintId)
        {
            var sprint = await _sprints.Query().AsNoTracking().FirstOrDefaultAsync(s => s.Id == sprintId) ?? throw new KeyNotFoundException();
            await _access.RequireAsync(caller, sprint.ProjectId, PmAction.View);

            // A planned sprint has no baseline yet - show what is currently planned as the starting scope.
            var committed = sprint.CommittedPoints
                ?? await _tasks.Query().Where(t => t.SprintId == sprintId).SumAsync(t => t.StoryPoints ?? 0);

            var pointed = await _tasks.Query().AsNoTracking()
                .Where(t => t.SprintId == sprintId && t.StoryPoints > 0)
                .Select(t => new { t.Id, Points = t.StoryPoints!.Value }).ToListAsync();
            var ids = pointed.Select(p => p.Id).ToList();
            var transitions = await _history.Query().AsNoTracking()
                .Where(h => ids.Contains(h.TaskId))
                .Select(h => new { h.TaskId, h.ToStatus, h.ChangedAt }).ToListAsync();
            var byTask = transitions.GroupBy(h => h.TaskId).ToDictionary(g => g.Key, g => g.OrderBy(h => h.ChangedAt).ToList());

            var start = sprint.StartDate.Date;
            var end = sprint.EndDate.Date;
            var today = DateTime.UtcNow.Date;
            var days = Math.Max(1, (end - start).Days);
            var points = new List<BurndownPointDto>();

            for (var d = start; d <= end; d = d.AddDays(1))
            {
                var index = (d - start).Days;
                var point = new BurndownPointDto { Date = d, Ideal = Math.Round(committed * (1 - (double)index / days), 2) };

                var showActual = sprint.State != SprintStates.Planned && d <= (sprint.State == SprintStates.Completed ? end : today);
                if (showActual)
                {
                    var cutoff = d.AddDays(1);
                    var done = pointed.Where(p =>
                        byTask.TryGetValue(p.Id, out var rows) &&
                        rows.LastOrDefault(r => r.ChangedAt < cutoff)?.ToStatus == "completed").Sum(p => p.Points);
                    point.Remaining = Math.Max(0, committed - done);
                }
                points.Add(point);
            }

            return new BurndownDto { SprintId = sprint.Id, Name = sprint.Name, State = sprint.State, CommittedPoints = committed, Points = points };
        }

        public async Task<VelocityDto> GetVelocityAsync(PmCaller caller, int projectId, int count)
        {
            await _access.RequireAsync(caller, projectId, PmAction.View);
            count = Math.Clamp(count, 1, 24);

            var recent = await _sprints.Query().AsNoTracking()
                .Where(s => s.ProjectId == projectId && s.State == SprintStates.Completed)
                .OrderByDescending(s => s.CompletedAt)
                .Take(count)
                .Select(s => new VelocityPointDto
                {
                    SprintId = s.Id, Name = s.Name,
                    CommittedPoints = s.CommittedPoints ?? 0, CompletedPoints = s.CompletedPoints ?? 0,
                })
                .ToListAsync();
            recent.Reverse(); // oldest first for charting

            return new VelocityDto
            {
                Sprints = recent,
                AverageCompleted = recent.Count == 0 ? 0 : Math.Round(recent.Average(s => s.CompletedPoints), 1),
            };
        }

        private async Task<Sprint> LoadAsync(int sprintId) =>
            await _sprints.Query().FirstOrDefaultAsync(s => s.Id == sprintId) ?? throw new KeyNotFoundException();

        private async Task<SprintDto> GetDtoAsync(int sprintId) =>
            await ProjectDto(_sprints.Query().AsNoTracking().Where(s => s.Id == sprintId)).FirstAsync();

        private static IQueryable<SprintDto> ProjectDto(IQueryable<Sprint> query) =>
            query.Select(s => new SprintDto
            {
                Id = s.Id,
                ProjectId = s.ProjectId,
                Name = s.Name,
                Goal = s.Goal,
                StartDate = s.StartDate,
                EndDate = s.EndDate,
                State = s.State,
                Capacity = s.Capacity,
                ItemCount = s.Tasks.Count(),
                TotalPoints = s.Tasks.Sum(t => t.StoryPoints ?? 0),
                DonePoints = s.Tasks.Where(t => t.Status == "completed").Sum(t => t.StoryPoints ?? 0),
                CommittedPoints = s.CommittedPoints,
                CompletedPoints = s.CompletedPoints,
                StartedAt = s.StartedAt,
                CompletedAt = s.CompletedAt,
            });
    }
}
