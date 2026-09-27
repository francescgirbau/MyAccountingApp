namespace MyAccountingApp.Web.Components;

/// <summary>Form payload raised by the edit option-transaction dialog.</summary>
public sealed record OptionTransactionFormValue(
    DateTime Date,
    string Description,
    decimal Amount,
    string Currency,
    string Symbol,
    string Isin,
    decimal Quantity,
    string Type);
