using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using Microsoft.EntityFrameworkCore;

namespace KhoiProjectManagement.Application
{
    public interface IPmBoardService
    {
        Task<BoardDto> GetBoardAsync(PmCaller caller, int projectId, int? sprintId = null);
        Task<BoardDto> SaveColumnsAsync(PmCaller caller, int projectId, List<SaveBoardColumnDto> columns);
        Task<WorkItemDto> MoveAsync(PmCaller caller, int itemId, MoveWorkItemDto dto);
    }

    // Kanban board over the project's work items. Columns map 1:1 to the existing statuses (renaming,
    // reordering and WIP limits only - configurable workflows are a later phase), and a move changes the
    // item's real status + rank, so there is no second copy of the work.
    public class PmBoardService : IPmBoardService
    {
        // Containers (Epic/Feature) live on the backlog; the board shows the work that actually flows.
        private static readonly string[] NonBoardTypes = { "Epic", "Feature" };

        private static readonly (string Status, string Name)[] Defaults =
            { ("todo", "To do"), ("in-progress", "In progress"), ("blocked", "Blocked"), ("completed", "Done") };

        private readonly IRepository<BoardColumn> _columns;
        private readonly IRepository<ProjectTask> _tasks;
        private readonly IRepository<TaskStatusHistory> _history;
        private readonly IPmWorkItemService _workItems;
        private readonly IPmWorkflowService _workflow;
        private readonly IProjectAccessService _access;
        private readonly IActivityLogService _activity;
        private readonly IUnitOfWork _unitOfWork;

        public PmBoardService(
            IRepository<BoardColumn> columns,
            IRepository<ProjectTask> tasks,
            IRepository<TaskStatusHistory> history,
            IPmWorkItemService workItems,
            IPmWorkflowService workflow,
            IProjectAccessService access,
            IActivityLogService activity,
            IUnitOfWork unitOfWork)
        {
            _columns = columns;
            _tasks = tasks;
            _history = history;
            _workItems = workItems;
            _workflow = workflow;
            _access = access;
            _activity = activity;
            _unitOfWork = unitOfWork;
        }

        public async Task<BoardDto> GetBoardAsync(PmCaller caller, int projectId, int? sprintId = null)
        {
            await _access.RequireAsync(caller, projectId, PmAction.View);
            var columns = await EnsureColumnsAsync(projectId);

            var items = // The board never shows Epic/Feature progress, so skip that extra project-wide query.
            await _workItems.GetBacklogAsync(caller, projectId, new WorkItemFilterDto { SprintId = sprintId }, includeRollup: false);
            var boardItems = items.Where(i => !NonBoardTypes.Contains(i.Type)).ToList(); // already rank-ordered

            return new BoardDto
            {
                ProjectId = projectId,
                Columns = columns.Select(c =>
                {
                    var cards = boardItems.Where(i => i.Status == c.Status).ToList();
                    return new BoardColumnDto
                    {
                        Id = c.Id, Status = c.Status, Name = c.Name, Position = c.Position, WipLimit = c.WipLimit,
                        Count = cards.Count, Items = cards,
                    };
                }).ToList(),
            };
        }

        public async Task<BoardDto> SaveColumnsAsync(PmCaller caller, int projectId, List<SaveBoardColumnDto> columns)
        {
            await _access.RequireAsync(caller, projectId, PmAction.ManageBoard);

            var statuses = columns.Select(c => c.Status).ToList();
            if (statuses.Count != TaskStatusRule.Valid.Length || statuses.Distinct().Count() != statuses.Count)
                throw new InvalidOperationException("The board must have exactly one column for each status.");

            var existing = await EnsureColumnsAsync(projectId);
            for (var i = 0; i < columns.Count; i++)
            {
                var column = existing.First(c => c.Status == columns[i].Status);
                column.Name = columns[i].Name.Trim();
                column.WipLimit = columns[i].WipLimit;
                column.Position = i;
            }
            await _unitOfWork.SaveChangesAsync();
            await _activity.LogAsync("Project", projectId, "Board columns", caller.UserId, "board-configured");
            return await GetBoardAsync(caller, projectId);
        }

        public async Task<WorkItemDto> MoveAsync(PmCaller caller, int itemId, MoveWorkItemDto dto)
        {
            var item = await _tasks.Query().FirstOrDefaultAsync(t => t.Id == itemId) ?? throw new KeyNotFoundException();
            await _access.RequireAsync(caller, item.ProjectId, PmAction.EditItem);

            if (NonBoardTypes.Contains(item.Type))
                throw new InvalidOperationException($"{item.Type} items are managed on the backlog, not the board.");

            var columns = await EnsureColumnsAsync(item.ProjectId);
            var target = columns.First(c => c.Status == dto.Status);
            var oldStatus = item.Status;
            await _workflow.EnsureTransitionAllowedAsync(item.ProjectId, oldStatus, dto.Status);

            // WIP applies to work entering a column; reordering inside one is always allowed.
            if (oldStatus != dto.Status && target.WipLimit.HasValue)
            {
                var inColumn = await _tasks.Query().CountAsync(t =>
                    t.ProjectId == item.ProjectId && t.Status == dto.Status && !NonBoardTypes.Contains(t.Type));
                if (inColumn >= target.WipLimit.Value)
                    throw new InvalidOperationException($"WIP limit reached for \"{target.Name}\" ({inColumn}/{target.WipLimit}). Finish or move something out first.");
            }

            var columnPeers = _tasks.Query().Where(t =>
                t.ProjectId == item.ProjectId && t.Id != itemId && t.Status == dto.Status && !NonBoardTypes.Contains(t.Type));
            item.Rank = await RankCalculator.ComputeAsync(columnPeers, dto.AfterItemId, "The card to place after is not in that column.");

            if (oldStatus != dto.Status)
            {
                item.Status = dto.Status;
                WorkItemStatusTracker.Record(item, oldStatus, dto.Status, caller.UserId, _history);
                item.CompletedAt = dto.Status == "completed" ? DateTime.UtcNow : null;
            }
            item.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.SaveChangesAsync();
            if (oldStatus != dto.Status)
                await _activity.LogAsync("WorkItem", item.Id, item.Title, caller.UserId, "moved", $"{oldStatus} -> {dto.Status}");
            return await _workItems.GetAsync(caller, itemId);
        }

        // Lazily seeds the four default columns the first time a project's board is touched.
        private async Task<List<BoardColumn>> EnsureColumnsAsync(int projectId)
        {
            var existing = await _columns.Query().Where(c => c.ProjectId == projectId).OrderBy(c => c.Position).ToListAsync();
            if (existing.Count > 0) return existing;

            existing = Defaults.Select((d, i) => new BoardColumn { ProjectId = projectId, Status = d.Status, Name = d.Name, Position = i }).ToList();
            _columns.AddRange(existing);
            await _unitOfWork.SaveChangesAsync();
            return existing;
        }
    }
}
