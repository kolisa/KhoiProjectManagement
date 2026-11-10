using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using Microsoft.EntityFrameworkCore;

namespace KhoiProjectManagement.Application
{
    public class ActivityLogService : IActivityLogService
    {
        private readonly IRepository<ActivityLogEntry> _logRepo;
        private readonly IRepository<User> _userRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IProjectScope _scope;
        private readonly IRepository<ProjectTask> _taskRepo;

        public ActivityLogService(IRepository<ActivityLogEntry> logRepo, IRepository<User> userRepo, IUnitOfWork unitOfWork,
            IProjectScope scope, IRepository<ProjectTask> taskRepo)
        {
            _scope = scope;
            _taskRepo = taskRepo;
            _logRepo = logRepo;
            _userRepo = userRepo;
            _unitOfWork = unitOfWork;
        }

        public async Task LogAsync(string entityType, int? entityId, string entityNameSnapshot, int actorUserId, string action, string? details = null)
        {
            var actor = await _userRepo.FindAsync(actorUserId);

            _logRepo.Add(new ActivityLogEntry
            {
                EntityType = entityType,
                EntityId = entityId,
                EntityNameSnapshot = entityNameSnapshot,
                ActorUserId = actorUserId,
                ActorNameSnapshot = actor?.Name ?? "Someone",
                Action = action,
                Details = details,
                Timestamp = DateTime.UtcNow
            });

            await _unitOfWork.SaveChangesAsync();
        }

        public async Task<List<ActivityLogEntryDto>> GetRecentAsync(int take = 20)
        {
            var visibleProjects = await _scope.VisibleProjectIdsAsync();
            if (visibleProjects != null)
                return await GetRecentForProjectsAsync(take, visibleProjects);

            return await _logRepo.Query()
                .OrderByDescending(a => a.Timestamp)
                .Take(take)
                .Select(a => new ActivityLogEntryDto
                {
                    Id = a.Id,
                    EntityType = a.EntityType,
                    EntityId = a.EntityId,
                    EntityNameSnapshot = a.EntityNameSnapshot,
                    ActorNameSnapshot = a.ActorNameSnapshot,
                    Action = a.Action,
                    Details = a.Details,
                    Timestamp = a.Timestamp
                })
                .ToListAsync();
        }

        // The company-wide feed names tasks and projects, so for anyone who is not an administrator only entries about
        // THEIR projects survive.
        private async Task<List<ActivityLogEntryDto>> GetRecentForProjectsAsync(int take, IReadOnlyCollection<int> visibleProjects)
        {
            // The entries carry an entity id, not a project id, so they are read newest-first in batches and filtered until
            // enough visible ones are found (or the log, or a sane cap of batches, runs out) - a quiet project is never
            // starved just because other projects were busier.
            const int batchSize = 200, maxBatches = 25;
            var result = new List<ActivityLogEntryDto>();
            for (var batch = 0; batch < maxBatches && result.Count < take; batch++)
            {
                var candidates = await _logRepo.Query()
                    .OrderByDescending(a => a.Timestamp).ThenByDescending(a => a.Id)
                    .Skip(batch * batchSize).Take(batchSize)
                    .Select(a => new ActivityLogEntryDto
                    {
                        Id = a.Id,
                        EntityType = a.EntityType,
                        EntityId = a.EntityId,
                        EntityNameSnapshot = a.EntityNameSnapshot,
                        ActorNameSnapshot = a.ActorNameSnapshot,
                        Action = a.Action,
                        Details = a.Details,
                        Timestamp = a.Timestamp
                    })
                    .ToListAsync();
                if (candidates.Count == 0) break;

                var taskIds = candidates.Where(c => c.EntityType is "WorkItem" or "Task" && c.EntityId != null)
                    .Select(c => c.EntityId!.Value).Distinct().ToList();
                var projectOfTask = taskIds.Count == 0
                    ? new Dictionary<int, int>()
                    : (await _taskRepo.Query().Where(t => taskIds.Contains(t.Id)).Select(t => new { t.Id, t.ProjectId }).ToListAsync())
                        .ToDictionary(t => t.Id, t => t.ProjectId);

                bool Visible(ActivityLogEntryDto c) => c.EntityType switch
                {
                    "Project" => c.EntityId is int projectId && visibleProjects.Contains(projectId),
                    "WorkItem" or "Task" => c.EntityId is int taskId && projectOfTask.TryGetValue(taskId, out var owner) && visibleProjects.Contains(owner),
                    // Sprint events and module-access changes name things the caller has no business seeing.
                    "Sprint" or "User" => false,
                    _ => true,
                };

                result.AddRange(candidates.Where(Visible));
                if (candidates.Count < batchSize) break;
            }
            return result.Take(take).ToList();
        }
    }
}
