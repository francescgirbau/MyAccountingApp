namespace MyAccountingApp.Web.Components;

/// <summary>Request to look up a ticker by description, raised by the import result report.</summary>
public sealed record SymbolSearchRequest(Guid AssetId, string Description);

/// <summary>Picks a symbol from the lookup results, raised by the import result report.</summary>
public sealed record SymbolSelection(Guid AssetId, string Symbol);