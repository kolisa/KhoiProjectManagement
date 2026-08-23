namespace KhoiProjectManagement.Application
{
    public interface IVaultEncryptionService
    {
        string Encrypt(string plaintext);
        string Decrypt(string ciphertext);
    }
}
