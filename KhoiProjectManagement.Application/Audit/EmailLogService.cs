using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using Microsoft.EntityFrameworkCore;

namespace KhoiProjectManagement.Application
{
    public class EmailLogService : IEmailLogService
    {
        private readonly IRepository<EmailLog> _emailLogRepo;

        public EmailLogService(IRepository<EmailLog> emailLogRepo)
        {
            _emailLogRepo = emailLogRepo;
        }

        public async Task<List<EmailLogDto>> GetRecentAsync(int take = 200, bool? isSuccess = null, string? emailType = null, string? toEmailContains = null)
        {
            var query = _emailLogRepo.Query();

            if (isSuccess.HasValue)
                query = query.Where(e => e.IsSuccess == isSuccess.Value);
            if (!string.IsNullOrWhiteSpace(emailType))
                query = query.Where(e => e.EmailType == emailType);
            if (!string.IsNullOrWhiteSpace(toEmailContains))
                query = query.Where(e => e.ToEmail.Contains(toEmailContains));

            return await query
                .OrderByDescending(e => e.SentAt)
                .Take(take)
                .Select(e => new EmailLogDto
                {
                    Id = e.Id,
                    ToEmail = e.ToEmail,
                    Subject = e.Subject,
                    EmailType = e.EmailType,
                    SentAt = e.SentAt,
                    IsSuccess = e.IsSuccess,
                    ErrorMessage = e.ErrorMessage
                })
                .ToListAsync();
        }
    }
}
