namespace MyAccountingApp.Web.Components;

/// <summary>Confirmation payload raised by the duplicate resolution dialog: the transaction the user chose to keep.</summary>
public sealed record ResolveDuplicatesInput(Guid KeeperId);
