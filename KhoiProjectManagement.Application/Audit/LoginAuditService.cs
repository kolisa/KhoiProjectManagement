using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using Microsoft.EntityFrameworkCore;

namespace KhoiProjectManagement.Application
{
    public class LoginAuditService : ILoginAuditService
    {
        private readonly IRepository<LoginAuditLog> _loginAuditRepo;
        private readonly IUnitOfWork _unitOfWork;

        public LoginAuditService(IRepository<LoginAuditLog> loginAuditRepo, IUnitOfWork unitOfWork)
        {
            _loginAuditRepo = loginAuditRepo;
            _unitOfWork = unitOfWork;
        }

        public async Task LogAsync(int? userId, string emailAttempted, bool success, string? failureReason, string? ipAddress)
        {
            _loginAuditRepo.Add(new LoginAuditLog
            {
                UserId = userId,
                EmailAttempted = emailAttempted,
                Success = success,
                FailureReason = failureReason,
                IpAddress = ipAddress
            });

            await _unitOfWork.SaveChangesAsync();
        }

        public async Task<List<LoginAuditLogDto>> GetRecentAsync(int take = 200, bool? success = null, string? emailContains = null)
        {
            var query = _loginAuditRepo.Query();

            if (success.HasValue)
                query = query.Where(a => a.Success == success.Value);
            if (!string.IsNullOrWhiteSpace(emailContains))
                query = query.Where(a => a.EmailAttempted.Contains(emailContains));

            return await query
                .OrderByDescending(a => a.Timestamp)
                .Take(take)
                .Select(a => new LoginAuditLogDto
                {
                    Id = a.Id,
                    UserId = a.UserId,
                    UserName = a.User != null ? a.User.Name : null,
                    EmailAttempted = a.EmailAttempted,
                    Success = a.Success,
                    FailureReason = a.FailureReason,
                    IpAddress = a.IpAddress,
                    Timestamp = a.Timestamp
                })
                .ToListAsync();
        }
    }
}
