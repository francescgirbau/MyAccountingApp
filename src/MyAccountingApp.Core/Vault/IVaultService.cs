namespace MyAccountingApp.Core.Vault;

public interface IVaultService
{
    bool IsEnabled { get; }
    bool IsInitialized { get; }
    bool IsUnlocked { get; }
    void Initialize(string password);
    bool Unlock(string password);
    void Lock();
    byte[] Encrypt(byte[] plaintext);
    byte[] Decrypt(byte[] ciphertext);
}
