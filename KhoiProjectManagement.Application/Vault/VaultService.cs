using System.Security.Claims;
using KhoiProjectManagement.Application.Abstractions;
using KhoiProjectManagement.Domain;
using KhoiProjectManagement.Application;
using KhoiProjectManagement.Application.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace KhoiProjectManagement.Application
{
    public class VaultService : IVaultService
    {
        private readonly IRepository<VaultEntry> _vaultEntryRepo;
        private readonly IRepository<User> _userRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IVaultEncryptionService _encryptionService;
        private readonly IVaultAuditService _auditService;
        private readonly IAuthorizationService _authorizationService;

        public VaultService(
            IRepository<VaultEntry> vaultEntryRepo,
            IRepository<User> userRepo,
            IUnitOfWork unitOfWork,
            IVaultEncryptionService encryptionService,
            IVaultAuditService auditService,
            IAuthorizationService authorizationService)
        {
            _vaultEntryRepo = vaultEntryRepo;
            _userRepo = userRepo;
            _unitOfWork = unitOfWork;
            _encryptionService = encryptionService;
            _auditService = auditService;
            _authorizationService = authorizationService;
        }

        public async Task<List<VaultEntrySummaryDto>> GetEntriesAsync(int spaceId, ClaimsPrincipal caller)
        {
            await RequireSpaceAccessAsync(spaceId, caller, PermissionLevel.Read);

            var entries = await _vaultEntryRepo.Query()
                .Where(v => v.SpaceId == spaceId && v.IsActive)
                .OrderBy(v => v.Name)
                .ToListAsync();

            return entries.Select(v => new VaultEntrySummaryDto
            {
                Id = v.Id,
                Name = v.Name,
                SpaceId = v.SpaceId,
                SystemOrUrl = v.SystemOrUrl,
                Username = v.Username,
                CreatedAt = v.CreatedAt,
                UpdatedAt = v.UpdatedAt
            }).ToList();
        }

        public async Task<VaultEntryDetailDto?> GetEntryByIdAsync(int id, ClaimsPrincipal caller)
        {
            var entry = await _vaultEntryRepo.Query()
                .Include(v => v.Creator)
                .Include(v => v.Updater)
                .FirstOrDefaultAsync(v => v.Id == id && v.IsActive);
            if (entry == null)
                return null;

            await AuthorizeEntryAsync(entry, caller, PermissionLevel.Read);

            await _auditService.LogAsync(VaultAuditAction.Viewed, entry.Id, entry.Name, GetUserId(caller));

            return new VaultEntryDetailDto
            {
                Id = entry.Id,
                Name = entry.Name,
                SpaceId = entry.SpaceId,
                SystemOrUrl = entry.SystemOrUrl,
                Username = entry.Username,
                Notes = entry.EncryptedNotes != null ? _encryptionService.Decrypt(entry.EncryptedNotes) : null,
                CreatorName = entry.Creator?.Name ?? "Unknown",
                CreatedAt = entry.CreatedAt,
                UpdaterName = entry.Updater?.Name,
                UpdatedAt = entry.UpdatedAt
            };
        }

        public async Task<VaultSecretRevealDto?> RevealSecretAsync(int id, ClaimsPrincipal caller)
        {
            var entry = await _vaultEntryRepo.Query().FirstOrDefaultAsync(v => v.Id == id && v.IsActive);
            if (entry == null)
                return null;

            await AuthorizeEntryAsync(entry, caller, PermissionLevel.Read);

            await _auditService.LogAsync(VaultAuditAction.SecretRevealed, entry.Id, entry.Name, GetUserId(caller));

            return new VaultSecretRevealDto { SecretValue = _encryptionService.Decrypt(entry.EncryptedSecret) };
        }

        public async Task<VaultEntryDetailDto> CreateEntryAsync(CreateVaultEntryDto dto, ClaimsPrincipal caller)
        {
            await RequireSpaceAccessAsync(dto.SpaceId, caller, PermissionLevel.Write);

            var userId = GetUserId(caller);
            var entry = new VaultEntry
            {
                Name = dto.Name,
                SpaceId = dto.SpaceId,
                SystemOrUrl = dto.SystemOrUrl,
                Username = dto.Username,
                EncryptedSecret = _encryptionService.Encrypt(dto.SecretValue),
                EncryptedNotes = dto.Notes != null ? _encryptionService.Encrypt(dto.Notes) : null,
                CreatedBy = userId
            };

            _vaultEntryRepo.Add(entry);
            await _unitOfWork.SaveChangesAsync();

            await _auditService.LogAsync(VaultAuditAction.Created, entry.Id, entry.Name, userId);

            var creator = await _userRepo.FindAsync(userId);
            return new VaultEntryDetailDto
            {
                Id = entry.Id,
                Name = entry.Name,
                SpaceId = entry.SpaceId,
                SystemOrUrl = entry.SystemOrUrl,
                Username = entry.Username,
                Notes = dto.Notes,
                CreatorName = creator?.Name ?? "Unknown",
                CreatedAt = entry.CreatedAt
            };
        }

        // Cap chosen to comfortably cover a real secrets export (a team's entire .env/password-manager
        // dump) while bounding a single request's work - far short of anything a legitimate import
        // would ever hit.
        private const int MaxImportRows = 500;
        private const long MaxImportFileBytes = 2 * 1024 * 1024;

        public async Task<VaultImportResultDto> ImportEntriesAsync(int spaceId, IFormFile file, ClaimsPrincipal caller)
        {
            await RequireSpaceAccessAsync(spaceId, caller, PermissionLevel.Write);

            if (file == null || file.Length == 0)
                throw new InvalidOperationException("No file was uploaded.");
            if (file.Length > MaxImportFileBytes)
                throw new InvalidOperationException("File is too large - imports are limited to 2 MB.");

            string content;
            using (var reader = new StreamReader(file.OpenReadStream()))
            {
                content = await reader.ReadToEndAsync();
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            List<VaultImportRow> rows;
            try
            {
                rows = extension switch
                {
                    ".json" => VaultImportParser.ParseJson(content),
                    ".csv" => VaultImportParser.ParseCsv(content),
                    _ => VaultImportParser.ParseEnv(content), // .env, .txt, or no/unrecognized extension
                };
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException)
            {
                throw new InvalidOperationException($"Could not read this file: {ex.Message}");
            }

            var result = new VaultImportResultDto();
            if (rows.Count > MaxImportRows)
            {
                result.Errors.Add($"File contained {rows.Count} rows - only the first {MaxImportRows} were processed.");
                rows = rows.Take(MaxImportRows).ToList();
            }

            var userId = GetUserId(caller);
            var newEntries = new List<VaultEntry>();

            foreach (var row in rows)
            {
                var name = row.Name?.Trim();
                var secret = row.SecretValue;

                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(secret))
                {
                    result.Skipped++;
                    result.Errors.Add(string.IsNullOrWhiteSpace(name)
                        ? "Skipped a row with no name."
                        : $"Skipped \"{name}\": no secret value.");
                    continue;
                }

                if (name.Length > 200)
                {
                    result.Skipped++;
                    result.Errors.Add($"Skipped \"{name}\": name exceeds 200 characters.");
                    continue;
                }

                newEntries.Add(new VaultEntry
                {
                    Name = name,
                    SpaceId = spaceId,
                    SystemOrUrl = Truncate(row.SystemOrUrl, 500),
                    Username = Truncate(row.Username, 200),
                    EncryptedSecret = _encryptionService.Encrypt(secret),
                    EncryptedNotes = !string.IsNullOrEmpty(row.Notes) ? _encryptionService.Encrypt(Truncate(row.Notes, 4000)!) : null,
                    CreatedBy = userId
                });
            }

            if (newEntries.Count > 0)
            {
                _vaultEntryRepo.AddRange(newEntries);
                await _unitOfWork.SaveChangesAsync();

                foreach (var entry in newEntries)
                {
                    await _auditService.LogAsync(VaultAuditAction.Created, entry.Id, entry.Name, userId, "Imported from file");
                }
            }

            result.Imported = newEntries.Count;
            return result;
        }

        private static string? Truncate(string? value, int maxLength) =>
            string.IsNullOrEmpty(value) ? value : (value.Length > maxLength ? value[..maxLength] : value);

        public async Task<bool> UpdateEntryAsync(int id, UpdateVaultEntryDto dto, ClaimsPrincipal caller)
        {
            var entry = await _vaultEntryRepo.Query().FirstOrDefaultAsync(v => v.Id == id && v.IsActive);
            if (entry == null)
                return false;

            await AuthorizeEntryAsync(entry, caller, PermissionLevel.Write);

            var userId = GetUserId(caller);
            entry.Name = dto.Name;
            entry.SystemOrUrl = dto.SystemOrUrl;
            entry.Username = dto.Username;
            if (!string.IsNullOrEmpty(dto.SecretValue))
            {
                entry.EncryptedSecret = _encryptionService.Encrypt(dto.SecretValue);
            }
            entry.EncryptedNotes = dto.Notes != null ? _encryptionService.Encrypt(dto.Notes) : null;
            entry.UpdatedBy = userId;
            entry.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.SaveChangesAsync();
            await _auditService.LogAsync(VaultAuditAction.Updated, entry.Id, entry.Name, userId);
            return true;
        }

        public async Task<bool> DeleteEntryAsync(int id, ClaimsPrincipal caller)
        {
            var entry = await _vaultEntryRepo.Query().FirstOrDefaultAsync(v => v.Id == id && v.IsActive);
            if (entry == null)
                return false;

            await AuthorizeEntryAsync(entry, caller, PermissionLevel.Manage);

            entry.IsActive = false;
            await _unitOfWork.SaveChangesAsync();

            await _auditService.LogAsync(VaultAuditAction.Deleted, entry.Id, entry.Name, GetUserId(caller));
            return true;
        }

        public async Task<List<VaultAuditLogDto>?> GetAuditLogAsync(int id, ClaimsPrincipal caller)
        {
            var entry = await _vaultEntryRepo.Query().FirstOrDefaultAsync(v => v.Id == id);
            if (entry == null)
                return null;

            await AuthorizeEntryAsync(entry, caller, PermissionLevel.Manage);

            var logs = await _auditService.GetAuditLogAsync(id);
            return logs.Select(a => new VaultAuditLogDto
            {
                Id = a.Id,
                Action = a.Action.ToString(),
                UserName = a.User?.Name ?? "Unknown",
                Timestamp = a.Timestamp,
                IpAddress = a.IpAddress,
                Details = a.Details
            }).ToList();
        }

        // Every read/write/delete re-checks the caller's permission against the target entry's Space
        // inside the service (via resource-based authorization), not just the controller's blanket
        // [Authorize] - the direct fix for AttachmentService returning any attachment by id with no
        // ownership check at all.
        private async Task AuthorizeEntryAsync(VaultEntry entry, ClaimsPrincipal caller, PermissionLevel level)
        {
            var result = await _authorizationService.AuthorizeAsync(caller, entry, new SpacePermissionRequirement(level));
            if (!result.Succeeded)
            {
                await _auditService.LogAsync(VaultAuditAction.AccessDenied, entry.Id, entry.Name, GetUserId(caller), $"Required level: {level}");
                throw new UnauthorizedAccessException($"Caller lacks {level} access to vault entry {entry.Id}.");
            }
        }

        private async Task RequireSpaceAccessAsync(int spaceId, ClaimsPrincipal caller, PermissionLevel level)
        {
            // A Space itself isn't ISpaceScoped (it scopes itself), so authorize against a reference
            // shim rather than duplicating the resolver's resolution logic here.
            var result = await _authorizationService.AuthorizeAsync(caller, new SpaceReference(spaceId), new SpacePermissionRequirement(level));
            if (!result.Succeeded)
            {
                throw new UnauthorizedAccessException($"Caller lacks {level} access to space {spaceId}.");
            }
        }

        private static int GetUserId(ClaimsPrincipal caller)
        {
            var claim = caller.FindFirst(ClaimTypes.NameIdentifier)
                ?? throw new InvalidOperationException("Caller has no NameIdentifier claim.");
            return int.Parse(claim.Value);
        }
    }
}
