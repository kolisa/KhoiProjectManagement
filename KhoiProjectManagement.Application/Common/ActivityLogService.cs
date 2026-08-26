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

        public ActivityLogService(IRepository<ActivityLogEntry> logRepo, IRepository<User> userRepo, IUnitOfWork unitOfWork)
        {
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
    }
}
