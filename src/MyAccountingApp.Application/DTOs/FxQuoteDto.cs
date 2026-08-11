namespace MyAccountingApp.Application.DTOs;

/// <summary>
/// Represents a single currency quote with full traceability of the requested date
/// versus the actual rate date (which may differ when a stale fallback is used).
/// </summary>
public sealed record FxQuoteDto(
    DateOnly RequestedDate,
    DateOnly RateDate,
    string Base,
    string Quote,
    decimal Rate,
    bool IsStale,
    string Provider);
