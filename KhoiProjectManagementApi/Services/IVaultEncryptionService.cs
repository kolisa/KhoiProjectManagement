namespace KhoiProjectManagementApi.Services
{
    public interface IVaultEncryptionService
    {
        string Encrypt(string plaintext);
        string Decrypt(string ciphertext);
    }
}
