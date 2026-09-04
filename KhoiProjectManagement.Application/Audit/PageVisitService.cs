using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using Microsoft.EntityFrameworkCore;

namespace KhoiProjectManagement.Application
{
    public class PageVisitService : IPageVisitService
    {
        private readonly IRepository<PageVisitLog> _pageVisitRepo;
        private readonly IUnitOfWork _unitOfWork;

        public PageVisitService(IRepository<PageVisitLog> pageVisitRepo, IUnitOfWork unitOfWork)
        {
            _pageVisitRepo = pageVisitRepo;
            _unitOfWork = unitOfWork;
        }

        public async Task<int> LogAsync(int userId, string tabKey)
        {
            var log = new PageVisitLog
            {
                UserId = userId,
                TabKey = tabKey
            };
            _pageVisitRepo.Add(log);

            await _unitOfWork.SaveChangesAsync();
            return log.Id;
        }

        public async Task<List<PageVisitLogDto>> GetRecentAsync(int take = 200, int? userId = null, string? tabKey = null)
        {
            var query = _pageVisitRepo.Query();

            if (userId.HasValue)
                query = query.Where(v => v.UserId == userId.Value);
            if (!string.IsNullOrWhiteSpace(tabKey))
                query = query.Where(v => v.TabKey == tabKey);

            return await query
                .OrderByDescending(v => v.Timestamp)
                .Take(take)
                .Select(v => new PageVisitLogDto
                {
                    Id = v.Id,
                    UserId = v.UserId,
                    UserName = v.User.Name,
                    TabKey = v.TabKey,
                    Timestamp = v.Timestamp,
                    DurationSeconds = v.DurationSeconds
                })
                .ToListAsync();
        }

        public async Task RecordDurationAsync(int id, int userId, int durationSeconds)
        {
            var log = await _pageVisitRepo.FindAsync(id);
            if (log == null || log.UserId != userId) return;

            log.DurationSeconds = durationSeconds;
            await _unitOfWork.SaveChangesAsync();
        }
    }
}
