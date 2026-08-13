namespace MyAccountingApp.Core.Vault;

/// <summary>
/// Vault no-op used when the vault feature is disabled via configuration
/// (e.g. while the product is in development). Repositories keep operating
/// in plaintext mode, the API gate never triggers, and no auth screens are
/// shown to the user.
/// </summary>
public class DisabledVaultService : IVaultService
{
    public bool IsEnabled => false;

    public bool IsInitialized => false;

    public bool IsUnlocked => false;

    public void Initialize(string password)
    {
        throw new InvalidOperationException("The vault is disabled.");
    }

    public bool Unlock(string password) => false;

    public void Lock()
    {
    }

    public byte[] Encrypt(byte[] plaintext)
    {
        throw new InvalidOperationException("The vault is disabled.");
    }

    public byte[] Decrypt(byte[] ciphertext)
    {
        throw new InvalidOperationException("The vault is disabled.");
    }
}
