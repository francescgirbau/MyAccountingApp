namespace MyAccountingApp.Web.Components;

/// <summary>Form payload raised by the create/edit asset-transaction dialog.</summary>
public sealed record AssetTransactionFormValue(
    DateTime Date,
    string Description,
    decimal Amount,
    string Currency,
    string Category,
    string Symbol,
    decimal Quantity,
    string Type);