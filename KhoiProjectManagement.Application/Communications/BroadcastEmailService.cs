using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using Microsoft.EntityFrameworkCore;

namespace KhoiProjectManagement.Application
{
    public class BroadcastEmailService : IBroadcastEmailService
    {
        private readonly IRepository<User> _userRepo;
        private readonly IRepository<UserRole> _userRoleRepo;
        private readonly IEmailService _emailService;

        public BroadcastEmailService(IRepository<User> userRepo, IRepository<UserRole> userRoleRepo, IEmailService emailService)
        {
            _userRepo = userRepo;
            _userRoleRepo = userRoleRepo;
            _emailService = emailService;
        }

        public async Task<int> SendBroadcastAsync(BroadcastEmailDto dto)
        {
            var userIds = await _userRoleRepo.Query()
                .Where(ur => dto.RoleIds.Contains(ur.RoleId))
                .Select(ur => ur.UserId)
                .Distinct()
                .ToListAsync();

            var recipientEmails = await _userRepo.Query()
                .Where(u => userIds.Contains(u.Id) && u.IsActive)
                .Select(u => u.Email)
                .ToListAsync();

            // Plain text in (see BroadcastEmailDto's comment - the composer UI is a plain textarea, not
            // a rich-text editor), HTML-encoded then line-broken on the way out - same reasoning as
            // EmailService.SendMentionEmailAsync's own handling of free-form input, just done here
            // instead since a Body->bodyHtml transform is Application-layer business logic, not
            // Infrastructure's concern (every other Send*EmailAsync already receives ready-to-wrap HTML).
            var bodyHtml = "<p>" + System.Net.WebUtility.HtmlEncode(dto.Body).Replace("\n", "<br>") + "</p>";

            foreach (var email in recipientEmails)
            {
                await _emailService.SendBroadcastEmailAsync(email, dto.Subject, bodyHtml);
            }

            return recipientEmails.Count;
        }
    }
}
