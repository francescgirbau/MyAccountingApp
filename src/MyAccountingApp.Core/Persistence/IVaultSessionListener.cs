namespace MyAccountingApp.Core.Persistence;

/// <summary>
/// Listens to vault session changes to keep in-memory repositories in sync.
/// </summary>
public interface IVaultSessionListener
{
    /// <summary>
    /// Called after the vault has been successfully unlocked.
    /// </summary>
    void OnUnlocked();

    /// <summary>
    /// Called after the vault has been locked.
    /// </summary>
    void OnLocked();
}
