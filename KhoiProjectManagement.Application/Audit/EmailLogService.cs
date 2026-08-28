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

        public async Task<List<EmailLogDto>> GetRecentAsync(int take = 200, string? status = null, string? emailType = null, string? toEmailContains = null)
        {
            var query = _emailLogRepo.Query();

            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<EmailLogStatus>(status, ignoreCase: true, out var parsedStatus))
                query = query.Where(e => e.Status == parsedStatus);
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
                    ErrorMessage = e.ErrorMessage,
                    Status = e.Status.ToString()
                })
                .ToListAsync();
        }
    }
}
