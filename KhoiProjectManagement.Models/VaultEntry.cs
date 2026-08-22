using System.ComponentModel.DataAnnotations;

namespace KhoiProjectManagement.Models
{
    // A vault "category" is just a Space with SpaceType.VaultCategory - access to a VaultEntry is
    // entirely governed by its Space's SpacePermission grants, resolved by the same
    // ISpacePermissionResolver every other Space-scoped item uses. No vault-specific authorization code.
    public class VaultEntry : ISpaceScoped
    {
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        public int SpaceId { get; set; }
        public virtual Space Space { get; set; } = null!;

        [StringLength(500)]
        public string? SystemOrUrl { get; set; }

        [StringLength(200)]
        public string? Username { get; set; }

        // Ciphertext produced by IVaultEncryptionService - never the plaintext secret.
        [Required]
        public string EncryptedSecret { get; set; } = string.Empty;

        public string? EncryptedNotes { get; set; }

        public int CreatedBy { get; set; }
        public virtual User Creator { get; set; } = null!;

        public int? UpdatedBy { get; set; }
        public virtual User? Updater { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Soft delete - so VaultAuditLog rows always resolve to something even after removal.
        public bool IsActive { get; set; } = true;
    }
}
