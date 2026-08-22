using System.Security.Claims;
using KhoiProjectManagement.Models.DTOs;

namespace KhoiProjectManagementApi.Services
{
    public interface IVaultService
    {
        // Throws UnauthorizedAccessException if the caller lacks at least Read on spaceId.
        Task<List<VaultEntrySummaryDto>> GetEntriesAsync(int spaceId, ClaimsPrincipal caller);

        // Returns null if the entry doesn't exist; throws UnauthorizedAccessException if the caller
        // lacks at least Read on the entry's Space. Logs a Viewed audit event on success.
        Task<VaultEntryDetailDto?> GetEntryByIdAsync(int id, ClaimsPrincipal caller);

        // The one endpoint that returns the decrypted secret - deliberately separate from
        // GetEntryByIdAsync so the sensitive decrypt path is individually audited. Logs SecretRevealed.
        Task<VaultSecretRevealDto?> RevealSecretAsync(int id, ClaimsPrincipal caller);

        Task<VaultEntryDetailDto> CreateEntryAsync(CreateVaultEntryDto dto, ClaimsPrincipal caller);
        Task<bool> UpdateEntryAsync(int id, UpdateVaultEntryDto dto, ClaimsPrincipal caller);
        Task<bool> DeleteEntryAsync(int id, ClaimsPrincipal caller);

        Task<List<VaultAuditLogDto>?> GetAuditLogAsync(int id, ClaimsPrincipal caller);
    }
}
