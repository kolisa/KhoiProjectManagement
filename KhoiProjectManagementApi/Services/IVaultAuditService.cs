using KhoiProjectManagement.Models;

namespace KhoiProjectManagementApi.Services
{
    public interface IVaultAuditService
    {
        Task LogAsync(VaultAuditAction action, int? vaultEntryId, string entryNameSnapshot, int userId, string? details = null);
        Task<List<VaultAuditLog>> GetAuditLogAsync(int vaultEntryId);
    }
}
