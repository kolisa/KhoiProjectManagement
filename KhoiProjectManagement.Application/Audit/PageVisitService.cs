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

        public async Task LogAsync(int userId, string tabKey)
        {
            _pageVisitRepo.Add(new PageVisitLog
            {
                UserId = userId,
                TabKey = tabKey
            });

            await _unitOfWork.SaveChangesAsync();
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
                    Timestamp = v.Timestamp
                })
                .ToListAsync();
        }
    }
}
