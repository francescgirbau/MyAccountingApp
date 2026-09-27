namespace MyAccountingApp.Contracts;

public record ConversionDto(
    DateTime Date,
    string Source,
    Dictionary<string, decimal> Quotes,
    bool IsStale,
    DateTime RetrievedAtUtc,
    string SourceProvider);