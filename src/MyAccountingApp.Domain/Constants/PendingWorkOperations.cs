namespace MyAccountingApp.Domain.Constants;

/// <summary>
/// Well-known pending work operation names.
/// </summary>
public static class PendingWorkOperations
{
    /// <summary>
    /// Operation that fetches currency conversion rates for queued dates.
    /// </summary>
    public const string CurrencyRate = "CurrencyRate";
}