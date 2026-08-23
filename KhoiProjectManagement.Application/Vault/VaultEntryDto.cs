namespace KhoiProjectManagement.Application
{
    // Metadata only - never the secret.
    public class VaultEntrySummaryDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int SpaceId { get; set; }
        public string? SystemOrUrl { get; set; }
        public string? Username { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    // Full metadata + decrypted notes - still no secret value (see VaultSecretRevealDto for that).
    public class VaultEntryDetailDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int SpaceId { get; set; }
        public string? SystemOrUrl { get; set; }
        public string? Username { get; set; }
        public string? Notes { get; set; }
        public string CreatorName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string? UpdaterName { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class CreateVaultEntryDto
    {
        public string Name { get; set; } = string.Empty;
        public int SpaceId { get; set; }
        public string? SystemOrUrl { get; set; }
        public string? Username { get; set; }
        public string SecretValue { get; set; } = string.Empty;
        public string? Notes { get; set; }
    }

    public class UpdateVaultEntryDto
    {
        public string Name { get; set; } = string.Empty;
        public string? SystemOrUrl { get; set; }
        public string? Username { get; set; }

        // Null/empty means "leave the existing secret unchanged".
        public string? SecretValue { get; set; }
        public string? Notes { get; set; }
    }

    public class VaultSecretRevealDto
    {
        public string SecretValue { get; set; } = string.Empty;
    }

    public class VaultAuditLogDto
    {
        public int Id { get; set; }
        public string Action { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string? IpAddress { get; set; }
        public string? Details { get; set; }
    }
}
