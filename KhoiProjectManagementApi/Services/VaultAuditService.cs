using KhoiProjectManagement.Models;
using KhoiProjectManagementApi.Data;
using Microsoft.EntityFrameworkCore;

namespace KhoiProjectManagementApi.Services
{
    // Called from every VaultService method - auditing is never left to the controller to remember.
    public class VaultAuditService : IVaultAuditService
    {
        private readonly ProjectManagementContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public VaultAuditService(ProjectManagementContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task LogAsync(VaultAuditAction action, int? vaultEntryId, string entryNameSnapshot, int userId, string? details = null)
        {
            var ipAddress = _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString();

            _context.VaultAuditLogs.Add(new VaultAuditLog
            {
                VaultEntryId = vaultEntryId,
                EntryNameSnapshot = entryNameSnapshot,
                UserId = userId,
                Action = action,
                IpAddress = ipAddress,
                Details = details
            });

            await _context.SaveChangesAsync();
        }

        public async Task<List<VaultAuditLog>> GetAuditLogAsync(int vaultEntryId)
        {
            return await _context.VaultAuditLogs
                .Where(a => a.VaultEntryId == vaultEntryId)
                .Include(a => a.User)
                .OrderByDescending(a => a.Timestamp)
                .ToListAsync();
        }
    }
}
