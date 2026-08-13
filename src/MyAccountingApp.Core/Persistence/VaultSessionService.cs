namespace MyAccountingApp.Core.Persistence;

/// <summary>
/// Reloads in-memory repository data when the vault is unlocked and clears it when locked,
/// so that plaintext data is only kept in memory while the vault session is open.
/// </summary>
public class VaultSessionService : IVaultSessionListener
{
    private readonly CompositeConversionRepository _conversionRepo;
    private readonly CompositeTransactionRepository _transactionRepo;
    private readonly CompositePortfolioRepository _portfolioRepo;

    /// <summary>
    /// Initializes a new instance of the <see cref="VaultSessionService"/> class.
    /// </summary>
    /// <param name="conversionRepo">The composite conversion repository.</param>
    /// <param name="transactionRepo">The composite transaction repository.</param>
    /// <param name="portfolioRepo">The composite portfolio repository.</param>
    public VaultSessionService(
        CompositeConversionRepository conversionRepo,
        CompositeTransactionRepository transactionRepo,
        CompositePortfolioRepository portfolioRepo)
    {
        this._conversionRepo = conversionRepo;
        this._transactionRepo = transactionRepo;
        this._portfolioRepo = portfolioRepo;
    }

    /// <inheritdoc/>
    public void OnUnlocked()
    {
        this._conversionRepo.Reload();
        this._transactionRepo.Reload();
        this._portfolioRepo.Reload();
    }

    /// <inheritdoc/>
    public void OnLocked()
    {
        this._conversionRepo.Clear();
        this._transactionRepo.Clear();
        this._portfolioRepo.Clear();
    }
}
