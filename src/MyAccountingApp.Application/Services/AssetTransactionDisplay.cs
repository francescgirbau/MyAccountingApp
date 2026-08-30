using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;

namespace MyAccountingApp.Application.Services;

/// <summary>
/// Presentation helpers for asset transactions: human-facing labels, explanatory tooltips
/// and drill-down deep links used across the web UI. Keeps the technical category accessible
/// while prioritizing the human label.
/// </summary>
public static class AssetTransactionDisplay
{
    /// <summary>
    /// Returns the human-facing label for an asset transaction type and technical category.
    /// Falls back to the technical category when the combination is not one of the known flows.
    /// </summary>
    public static string GetCategoryLabel(string type, string category) => (type, category) switch
    {
        ("Buy", "INVESTMENT") => "Asset purchase",
        ("Sell", "DIVESTMENT") => "Asset sale",
        _ => category,
    };

    /// <summary>
    /// Returns an explanatory tooltip for an asset transaction, clarifying that buying/selling
    /// an asset is investing activity rather than operating income or expense.
    /// </summary>
    public static string GetCategoryTooltip(string type, string category) => (type, category) switch
    {
        ("Buy", "INVESTMENT") => "An asset purchase converts cash into an asset; it is not an operating expense.",
        ("Sell", "DIVESTMENT") => "Asset-sale proceeds are not operating income. Realized gains are calculated separately from FIFO when available.",
        _ => string.Empty,
    };

    /// <summary>
    /// Builds the Asset Transactions deep link for a given year and flow (purchase or sale),
    /// pre-filling the year and type filters. Purchase maps to Buy, sale maps to Sell.
    /// </summary>
    public static string BuildDeepLink(int year, bool purchase) =>
        $"/asset-transactions?year={year}&type={(purchase ? "Buy" : "Sell")}";

    /// <summary>
    /// Whether an asset transaction belongs to the requested year and flow (purchase or sale).
    /// Used to apply the year + type filters that come from a drill-down deep link.
    /// </summary>
    public static bool MatchesFilter(AssetTransaction transaction, int year, bool purchase)
    {
        bool matchesYear = transaction.Transaction.Date.Year == year;
        bool matchesFlow = purchase
            ? transaction.Type == AssetTransactionType.Buy
            : transaction.Type == AssetTransactionType.Sell;
        return matchesYear && matchesFlow;
    }
}
