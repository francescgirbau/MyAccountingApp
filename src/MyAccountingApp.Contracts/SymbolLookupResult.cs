namespace MyAccountingApp.Contracts;

public sealed record SymbolLookupResult(
    string Symbol,
    string Name,
    string Exchange,
    string Type);