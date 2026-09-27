namespace MyAccountingApp.Web.Components;

/// <summary>Form payload raised by the create/edit transaction dialog.</summary>
public sealed record TransactionFormValue(
    DateTime Date,
    string Description,
    decimal Amount,
    string Currency,
    string Category);