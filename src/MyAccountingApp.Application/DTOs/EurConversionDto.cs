namespace MyAccountingApp.Application.DTOs;

/// <summary>
/// Represents a currency amount converted to EUR with full FX traceability.
/// </summary>
public sealed record EurConversionDto(
    decimal AmountEur,
    decimal Rate,
    DateOnly RateDate,
    bool IsStale,
    string Provider);
