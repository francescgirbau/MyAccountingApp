namespace MyAccountingApp.Domain.Enums;
public enum AssetTransactionType
{
    /// <summary>
    /// Add an asset purchase transaction.
    /// </summary>
    Buy,

    /// <summary>
    /// Remove an asset sale transaction.
    /// </summary>
    Sell,
    /// <summary>
    /// It is the payment of a dividend.
    /// </summary>
    Dividend,
}
