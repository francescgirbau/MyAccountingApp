namespace MyAccountingApp.Web.Components;

/// <summary>Per-currency subtotals for the filtered asset-transaction list (presented in the Asset Transactions page).</summary>
public sealed record AssetTransactionSummary(
    string Currency,
    int Count,
    int BuyCount,
    decimal BuyCost,
    int SellCount,
    decimal SellProceeds,
    decimal Fees,
    decimal NetCashFlow);