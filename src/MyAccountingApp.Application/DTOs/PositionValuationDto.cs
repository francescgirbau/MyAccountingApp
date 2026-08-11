namespace MyAccountingApp.Application.DTOs;

/// <summary>
/// Represents the EUR valuation of an open position, including the FX rate applied and its rate date.
/// </summary>
public sealed record PositionValuationDto(
    string Symbol,
    string Currency,
    decimal NetQuantity,
    decimal? MarketPrice,
    decimal? UnrealizedGainLoss,
    decimal? ValueEur,
    decimal? UnrealizedGainLossEur,
    decimal? Rate,
    DateOnly? RateDate,
    bool IsStale,
    string? Provider);
