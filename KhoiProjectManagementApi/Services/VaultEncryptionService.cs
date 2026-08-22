using Microsoft.AspNetCore.DataProtection;

namespace KhoiProjectManagementApi.Services
{
    // Wraps ASP.NET Core Data Protection so VaultService never touches IDataProtector directly - a
    // seam for future replacement (e.g. a KMS-backed provider) without touching call sites.
    public class VaultEncryptionService : IVaultEncryptionService
    {
        private readonly IDataProtector _protector;

        public VaultEncryptionService(IDataProtectionProvider provider)
        {
            _protector = provider.CreateProtector("KhoiProjectManagement.Vault.v1");
        }

        public string Encrypt(string plaintext) => _protector.Protect(plaintext);

        public string Decrypt(string ciphertext) => _protector.Unprotect(ciphertext);
    }
}
