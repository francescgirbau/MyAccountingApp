namespace MyAccountingApp.Web.Components;

/// <summary>Per-currency subtotals for the filtered transaction list (presented in the Transactions page).</summary>
public sealed record FilterCurrencySummary(
    string Currency,
    int Count,
    decimal Income,
    decimal Expense,
    decimal InvestingPurchase,
    decimal InvestingSale,
    decimal Transfer,
    decimal Deposit,
    decimal LoanOut,
    decimal LoanIn,
    decimal FxOut,
    decimal FxIn,
    decimal OperatingNet);