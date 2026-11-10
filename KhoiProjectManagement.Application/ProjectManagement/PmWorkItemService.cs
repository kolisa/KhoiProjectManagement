using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KhoiProjectManagement.Application
{
    public interface IPmWorkItemService
    {
        // includeRollup: fills in the Epic/Feature progress fields. It costs one extra project-wide query, so
        // callers that never show it (the board) pass false.
        Task<List<WorkItemDto>> GetBacklogAsync(PmCaller caller, int projectId, WorkItemFilterDto filter, bool includeRollup = true);
        Task<List<WorkItemLookupDto>> GetLookupAsync(PmCaller caller, int projectId);
        Task<WorkItemDto> GetAsync(PmCaller caller, int itemId);
        Task<WorkItemDto> CreateAsync(PmCaller caller, int projectId, SaveWorkItemDto dto);
        Task<WorkItemDto> UpdateAsync(PmCaller caller, int itemId, SaveWorkItemDto dto);
        Task<WorkItemDto> RankAsync(PmCaller caller, int itemId, RankWorkItemDto dto);
        Task DeleteAsync(PmCaller caller, int itemId);
    }

    // Phase 1 of the PM module: the work-item hierarchy + ranked backlog on top of the existing
    // ProjectTask table (so the classic Tasks tab, notifications and reports keep seeing the same rows).
    public class PmWorkItemService : IPmWorkItemService
    {
        private const double RankStep = RankCalculator.Step;

        private readonly IRepository<ProjectTask> _tasks;
        private readonly IRepository<Project> _projects;
        private readonly IRepository<ProjectUser> _members;
        private readonly IRepository<TaskStatusHistory> _history;
        private readonly IRepository<Attachment> _attachments;
        private readonly IFileStorage _storage;
        private readonly IPmWorkflowService _workflow;
        private readonly ILogger<PmWorkItemService> _logger;
        private readonly IProjectAccessService _access;
        private readonly IActivityLogService _activity;
        private readonly IUnitOfWork _unitOfWork;

        public PmWorkItemService(
            IRepository<ProjectTask> tasks,
            IRepository<Project> projects,
            IRepository<ProjectUser> members,
            IRepository<TaskStatusHistory> history,
            IRepository<Attachment> attachments,
            IFileStorage storage,
            IPmWorkflowService workflow,
            IProjectAccessService access,
            IActivityLogService activity,
            IUnitOfWork unitOfWork,
            ILogger<PmWorkItemService> logger)
        {
            _tasks = tasks;
            _projects = projects;
            _members = members;
            _history = history;
            _attachments = attachments;
            _storage = storage;
            _workflow = workflow;
            _logger = logger;
            _access = access;
            _activity = activity;
            _unitOfWork = unitOfWork;
        }

        public async Task<List<WorkItemDto>> GetBacklogAsync(PmCaller caller, int projectId, WorkItemFilterDto filter, bool includeRollup = true)
        {
            await _access.RequireAsync(caller, projectId, PmAction.View);

            var query = ApplyFilter(_tasks.Query().AsNoTracking().Where(t => t.ProjectId == projectId), filter);

            var list = await Project(query.OrderBy(t => t.Rank).ThenBy(t => t.Id)).ToListAsync();
            if (includeRollup) await ApplyRollupAsync(projectId, list);
            return list;
        }

        public async Task<List<WorkItemLookupDto>> GetLookupAsync(PmCaller caller, int projectId)
        {
            await _access.RequireAsync(caller, projectId, PmAction.View);
            return await _tasks.Query().AsNoTracking().Where(t => t.ProjectId == projectId)
                .OrderBy(t => t.Rank).ThenBy(t => t.Id)
                .Select(t => new WorkItemLookupDto { Id = t.Id, Title = t.Title, Type = t.Type, Status = t.Status })
                .ToListAsync();
        }

        public async Task<WorkItemDto> GetAsync(PmCaller caller, int itemId)
        {
            var projectId = await ProjectIdOfAsync(itemId);
            await _access.RequireAsync(caller, projectId, PmAction.View);
            var dto = await Project(_tasks.Query().AsNoTracking().Where(t => t.Id == itemId)).FirstAsync();
            await ApplyRollupAsync(projectId, new[] { dto });
            return dto;
        }

        public async Task<WorkItemDto> CreateAsync(PmCaller caller, int projectId, SaveWorkItemDto dto)
        {
            await _access.RequireAsync(caller, projectId, PmAction.CreateItem);
            // A new item starts in To do, so creating one directly in another status is a To do -> X move
            // and must obey the same workflow rules.
            await _workflow.EnsureTransitionAllowedAsync(projectId, "todo", dto.Status);
            var project = await _projects.Query().AsNoTracking().FirstAsync(p => p.Id == projectId);

            await ValidateRelationsAsync(projectId, null, dto);

            var maxRank = await _tasks.Query().Where(t => t.ProjectId == projectId)
                .Select(t => (double?)t.Rank).MaxAsync() ?? 0;

            var item = new ProjectTask
            {
                ProjectId = projectId,
                Title = dto.Title.Trim(),
                Description = dto.Description,
                Type = dto.Type,
                Status = dto.Status,
                Priority = dto.Priority,
                StoryPoints = dto.StoryPoints,
                AssignedToId = dto.AssignedToId,
                ParentId = dto.ParentId,
                DueDate = dto.DueDate ?? project.EndDate,
                HasExplicitDueDate = dto.DueDate.HasValue,
                Rank = maxRank + RankStep,
                CompletedAt = dto.Status == "completed" ? DateTime.UtcNow : null,
            };
            _tasks.Add(item);
            WorkItemStatusTracker.Record(item, null, item.Status, caller.UserId, _history);
            await _unitOfWork.SaveChangesAsync();

            await _activity.LogAsync("WorkItem", item.Id, item.Title, caller.UserId, "created", $"{item.Type} in {project.Name}");
            return await GetAsync(caller, item.Id);
        }

        public async Task<WorkItemDto> UpdateAsync(PmCaller caller, int itemId, SaveWorkItemDto dto)
        {
            var item = await _tasks.Query().FirstOrDefaultAsync(t => t.Id == itemId) ?? throw new KeyNotFoundException();
            await _access.RequireAsync(caller, item.ProjectId, PmAction.EditItem);

            await ValidateRelationsAsync(item.ProjectId, item, dto);
            await _workflow.EnsureTransitionAllowedAsync(item.ProjectId, item.Status, dto.Status);

            // Changing Type must not strand existing children under a now-illegal parent.
            if (dto.Type != item.Type)
            {
                var childTypes = await _tasks.Query().Where(t => t.ParentId == itemId).Select(t => t.Type).Distinct().ToListAsync();
                foreach (var childType in childTypes)
                {
                    var error = WorkItemHierarchy.ValidateParent(childType, dto.Type);
                    if (error != null) throw new InvalidOperationException($"Cannot change type: existing {childType} children would be invalid. {error}");
                }
            }

            var oldStatus = item.Status;
            var wasCompleted = item.Status == "completed";
            item.Title = dto.Title.Trim();
            item.Description = dto.Description;
            item.Type = dto.Type;
            item.Status = dto.Status;
            item.Priority = dto.Priority;
            item.StoryPoints = dto.StoryPoints;
            item.AssignedToId = dto.AssignedToId;
            item.ParentId = dto.ParentId;
            if (dto.DueDate.HasValue)
            {
                item.DueDate = dto.DueDate.Value;
                item.HasExplicitDueDate = true;
            }
            item.UpdatedAt = DateTime.UtcNow;
            if (dto.Status == "completed" && !wasCompleted) item.CompletedAt = DateTime.UtcNow;
            else if (dto.Status != "completed") item.CompletedAt = null;
            WorkItemStatusTracker.Record(item, oldStatus, dto.Status, caller.UserId, _history);

            await _unitOfWork.SaveChangesAsync();
            await _activity.LogAsync("WorkItem", item.Id, item.Title, caller.UserId, "updated", item.Status);
            return await GetAsync(caller, item.Id);
        }

        public async Task<WorkItemDto> RankAsync(PmCaller caller, int itemId, RankWorkItemDto dto)
        {
            var item = await _tasks.Query().FirstOrDefaultAsync(t => t.Id == itemId) ?? throw new KeyNotFoundException();
            await _access.RequireAsync(caller, item.ProjectId, PmAction.RankItem);

            var others = _tasks.Query().Where(t => t.ProjectId == item.ProjectId && t.Id != itemId);

            item.Rank = await RankCalculator.ComputeAsync(others, dto.AfterItemId, "The item to place after is not in this project.");

            item.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync();
            return await GetAsync(caller, itemId);
        }

        public async Task DeleteAsync(PmCaller caller, int itemId)
        {
            var item = await _tasks.Query().FirstOrDefaultAsync(t => t.Id == itemId) ?? throw new KeyNotFoundException();
            await _access.RequireAsync(caller, item.ProjectId, PmAction.DeleteItem);

            if (await _tasks.Query().AnyAsync(t => t.ParentId == itemId))
                throw new InvalidOperationException("This item has child items. Move or delete them first.");

            // Attachments have no cascade (their Task link predates this module), so remove them - and the
            // stored files - explicitly. Comments, checklist items and links cascade in the database.
            var attachments = await _attachments.Query().Where(a => a.TaskId == itemId).ToListAsync();
            _attachments.RemoveRange(attachments);
            _tasks.Remove(item);
            await _unitOfWork.SaveChangesAsync();
            // The rows are already gone, so a failed object delete is logged, not surfaced.
            foreach (var attachment in attachments)
            {
                try
                {
                    await _storage.DeleteAsync(attachment.FilePath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not delete stored object {Key} for deleted work item {ItemId}", attachment.FilePath, itemId);
                }
            }
            await _activity.LogAsync("WorkItem", null, item.Title, caller.UserId, "deleted", item.Type);
        }

        // Parent/assignee rules shared by create and update. `existing` is null on create.
        private async Task ValidateRelationsAsync(int projectId, ProjectTask? existing, SaveWorkItemDto dto)
        {
            string? parentType = null;
            if (dto.ParentId.HasValue)
            {
                if (existing != null && dto.ParentId == existing.Id)
                    throw new InvalidOperationException("An item cannot be its own parent.");
                var parent = await _tasks.Query().AsNoTracking()
                    .Where(t => t.Id == dto.ParentId && t.ProjectId == projectId)
                    .Select(t => new { t.Type }).FirstOrDefaultAsync()
                    ?? throw new InvalidOperationException("The parent item does not exist in this project.");
                parentType = parent.Type;
            }

            var parentError = WorkItemHierarchy.ValidateParent(dto.Type, parentType);
            if (parentError != null) throw new InvalidOperationException(parentError);

            if (dto.AssignedToId.HasValue &&
                !await _members.Query().AnyAsync(m => m.ProjectId == projectId && m.UserId == dto.AssignedToId))
                throw new InvalidOperationException("The assignee is not a member of this project.");
        }

        // Fills in the Rollup* fields for the items that have children. Needs the whole project's (id, parent,
        // status, points) rows because a filtered list may not contain an item's descendants.
        private async Task ApplyRollupAsync(int projectId, IReadOnlyCollection<WorkItemDto> dtos)
        {
            if (!dtos.Any(d => d.ChildCount > 0)) return;

            var rows = await _tasks.Query().AsNoTracking().Where(t => t.ProjectId == projectId)
                .Select(t => new { t.Id, t.ParentId, t.Status, t.StoryPoints }).ToListAsync();
            var rollup = WorkItemRollup.Compute(rows.Select(r => new WorkItemRollup.Node(r.Id, r.ParentId, r.Status, r.StoryPoints)).ToList());

            foreach (var dto in dtos.Where(d => rollup.ContainsKey(d.Id)))
            {
                var r = rollup[dto.Id];
                dto.RollupDone = r.Done;
                dto.RollupTotal = r.Total;
                dto.RollupPointsDone = r.PointsDone;
                dto.RollupPointsTotal = r.PointsTotal;
            }
        }

        private async Task<int> ProjectIdOfAsync(int itemId) =>
            await _tasks.Query().AsNoTracking().Where(t => t.Id == itemId).Select(t => (int?)t.ProjectId).FirstOrDefaultAsync()
                ?? throw new KeyNotFoundException();

        // The backlog's filters, kept as a static query builder so the translation to SQL can be tested without
        // a database (see PmQueryTranslationTests).
        public static IQueryable<ProjectTask> ApplyFilter(IQueryable<ProjectTask> query, WorkItemFilterDto filter)
        {
            if (!string.IsNullOrWhiteSpace(filter.Type)) query = query.Where(t => t.Type == filter.Type);
            if (!string.IsNullOrWhiteSpace(filter.Status)) query = query.Where(t => t.Status == filter.Status);
            if (filter.AssigneeId.HasValue) query = query.Where(t => t.AssignedToId == filter.AssigneeId);
            if (filter.ParentId.HasValue) query = query.Where(t => t.ParentId == filter.ParentId);
            if (filter.SprintId.HasValue) query = query.Where(t => t.SprintId == filter.SprintId);
            if (filter.Unplanned == true) query = query.Where(t => t.SprintId == null);
            if (filter.LabelId.HasValue) query = query.Where(t => t.TaskTags.Any(x => x.TagId == filter.LabelId));
            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var term = filter.Search.Trim().ToLower();
                query = query.Where(t => t.Title.ToLower().Contains(term) || t.Description.ToLower().Contains(term));
            }
            return query;
        }

        public static IQueryable<WorkItemDto> Project(IQueryable<ProjectTask> query) =>
            query.Select(t => new WorkItemDto
            {
                Id = t.Id,
                ProjectId = t.ProjectId,
                Title = t.Title,
                Description = t.Description,
                Type = t.Type,
                Status = t.Status,
                Priority = t.Priority,
                StoryPoints = t.StoryPoints,
                StartedAt = t.StartedAt,
                SprintId = t.SprintId,
                SprintName = t.Sprint != null ? t.Sprint.Name : null,
                AssignedToId = t.AssignedToId,
                AssignedToName = t.AssignedTo != null ? t.AssignedTo.Name : null,
                ParentId = t.ParentId,
                ParentTitle = t.Parent != null ? t.Parent.Title : null,
                ChildCount = t.Children.Count,
                Labels = t.TaskTags.Select(x => new LabelDto { Id = x.TagId, Name = x.Tag.Name, Color = x.Tag.Color }).ToList(),
                DueDate = t.DueDate,
                Rank = t.Rank,
                HasDueDate = t.HasExplicitDueDate,
                IsOverdue = t.Status != "completed" && t.HasExplicitDueDate && t.DueDate < DateTime.UtcNow,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt,
                CompletedAt = t.CompletedAt,
            });
    }
}
