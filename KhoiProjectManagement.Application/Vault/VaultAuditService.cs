using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KhoiProjectManagement.Application
{
    // Called from every VaultService method - auditing is never left to the controller to remember.
    public class VaultAuditService : IVaultAuditService
    {
        private readonly IRepository<VaultAuditLog> _auditLogRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentRequestContext _requestContext;
        private readonly ILogger<VaultAuditService> _logger;

        public VaultAuditService(
            IRepository<VaultAuditLog> auditLogRepo,
            IUnitOfWork unitOfWork,
            ICurrentRequestContext requestContext,
            ILogger<VaultAuditService> logger)
        {
            _auditLogRepo = auditLogRepo;
            _unitOfWork = unitOfWork;
            _requestContext = requestContext;
            _logger = logger;
        }

        public async Task LogAsync(VaultAuditAction action, int? vaultEntryId, string entryNameSnapshot, int userId, string? details = null)
        {
            var ipAddress = _requestContext.RemoteIpAddress;

            _auditLogRepo.Add(new VaultAuditLog
            {
                VaultEntryId = vaultEntryId,
                EntryNameSnapshot = entryNameSnapshot,
                UserId = userId,
                Action = action,
                IpAddress = ipAddress,
                Details = details
            });

            await _unitOfWork.SaveChangesAsync();

            // VaultAuditLog (DB) is the source of truth for per-entry queries; mirroring to Serilog too
            // gives ops a searchable stream across entries/instances without querying the DB, and
            // SecretRevealed/AccessDenied specifically warrant a level above Information since they're
            // the two events most worth alerting on.
            var level = action is VaultAuditAction.SecretRevealed or VaultAuditAction.AccessDenied
                ? LogLevel.Warning
                : LogLevel.Information;
            _logger.Log(level, "Vault {Action}: entry {EntryName} (id={VaultEntryId}) by user {UserId} from {IpAddress}",
                action, entryNameSnapshot, vaultEntryId, userId, ipAddress ?? "unknown");
        }

        public async Task<List<VaultAuditLog>> GetAuditLogAsync(int vaultEntryId)
        {
            return await _auditLogRepo.Query()
                .Where(a => a.VaultEntryId == vaultEntryId)
                .Include(a => a.User)
                .OrderByDescending(a => a.Timestamp)
                .ToListAsync();
        }
    }
}
