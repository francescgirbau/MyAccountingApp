namespace MyAccountingApp.Contracts;

/// <summary>
/// A per-contract option position: all the legs of one option contract grouped together,
/// with open/close status, premium flows, capital at risk and return metrics.
/// </summary>
public sealed record OptionPositionDto(
    string Key,
    string Symbol,
    string Underlying,
    string Strategy,
    string Side,
    string Direction,
    DateOnly? Expiration,
    decimal? Strike,
    decimal Quantity,
    string Currency,
    DateOnly OpenDate,
    DateOnly? CloseDate,
    string Status,
    decimal Credit,
    decimal Debit,
    decimal ProfitLoss,
    decimal CapitalAtRisk,
    decimal? Yield,
    int DaysHeld,
    decimal? AnnualizedReturn);