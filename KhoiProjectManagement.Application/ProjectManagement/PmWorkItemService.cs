using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using Microsoft.EntityFrameworkCore;

namespace KhoiProjectManagement.Application
{
    public interface IPmWorkItemService
    {
        Task<List<WorkItemDto>> GetBacklogAsync(PmCaller caller, int projectId, WorkItemFilterDto filter);
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
        private readonly IProjectAccessService _access;
        private readonly IActivityLogService _activity;
        private readonly IUnitOfWork _unitOfWork;

        public PmWorkItemService(
            IRepository<ProjectTask> tasks,
            IRepository<Project> projects,
            IRepository<ProjectUser> members,
            IRepository<TaskStatusHistory> history,
            IProjectAccessService access,
            IActivityLogService activity,
            IUnitOfWork unitOfWork)
        {
            _tasks = tasks;
            _projects = projects;
            _members = members;
            _history = history;
            _access = access;
            _activity = activity;
            _unitOfWork = unitOfWork;
        }

        public async Task<List<WorkItemDto>> GetBacklogAsync(PmCaller caller, int projectId, WorkItemFilterDto filter)
        {
            await _access.RequireAsync(caller, projectId, PmAction.View);

            var query = _tasks.Query().AsNoTracking().Where(t => t.ProjectId == projectId);
            if (!string.IsNullOrWhiteSpace(filter.Type)) query = query.Where(t => t.Type == filter.Type);
            if (!string.IsNullOrWhiteSpace(filter.Status)) query = query.Where(t => t.Status == filter.Status);
            if (filter.AssigneeId.HasValue) query = query.Where(t => t.AssignedToId == filter.AssigneeId);
            if (filter.ParentId.HasValue) query = query.Where(t => t.ParentId == filter.ParentId);
            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var term = filter.Search.Trim().ToLower();
                query = query.Where(t => t.Title.ToLower().Contains(term) || t.Description.ToLower().Contains(term));
            }

            return await Project(query.OrderBy(t => t.Rank).ThenBy(t => t.Id)).ToListAsync();
        }

        public async Task<WorkItemDto> GetAsync(PmCaller caller, int itemId)
        {
            var projectId = await ProjectIdOfAsync(itemId);
            await _access.RequireAsync(caller, projectId, PmAction.View);
            return await Project(_tasks.Query().AsNoTracking().Where(t => t.Id == itemId)).FirstAsync();
        }

        public async Task<WorkItemDto> CreateAsync(PmCaller caller, int projectId, SaveWorkItemDto dto)
        {
            await _access.RequireAsync(caller, projectId, PmAction.CreateItem);
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
            if (dto.DueDate.HasValue) item.DueDate = dto.DueDate.Value;
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

            _tasks.Remove(item);
            await _unitOfWork.SaveChangesAsync();
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

        private async Task<int> ProjectIdOfAsync(int itemId) =>
            await _tasks.Query().AsNoTracking().Where(t => t.Id == itemId).Select(t => (int?)t.ProjectId).FirstOrDefaultAsync()
                ?? throw new KeyNotFoundException();

        private static IQueryable<WorkItemDto> Project(IQueryable<ProjectTask> query) =>
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
                AssignedToId = t.AssignedToId,
                AssignedToName = t.AssignedTo != null ? t.AssignedTo.Name : null,
                ParentId = t.ParentId,
                ParentTitle = t.Parent != null ? t.Parent.Title : null,
                ChildCount = t.Children.Count,
                DueDate = t.DueDate,
                Rank = t.Rank,
                IsOverdue = t.Status != "completed" && t.DueDate < DateTime.UtcNow,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt,
                CompletedAt = t.CompletedAt,
            });
    }
}
