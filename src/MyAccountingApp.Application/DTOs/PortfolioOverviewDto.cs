namespace MyAccountingApp.Application.DTOs;

public sealed record PortfolioOverviewDto(
    decimal MarketValueEur,
    decimal InvestedCostEur,
    decimal UnrealizedPnLEur,
    decimal? UnrealizedPnLPct,
    DateTimeOffset? PricesAsOfUtc,
    bool IsMarketClosed,
    int UnpricedPositionCount,
    int OptionSymbolCount,
    IReadOnlyList<PortfolioPositionRowDto> Positions,
    IReadOnlyList<AllocationSliceDto> PurchaseAllocation,
    IReadOnlyList<AllocationSliceDto> CurrentAllocation);

public sealed record PortfolioPositionRowDto(
    string Symbol,
    decimal Quantity,
    decimal Cost,
    string Currency,
    decimal? CostEur,
    decimal? MarketValue,
    decimal? MarketValueEur,
    decimal? UnrealizedPnL,
    decimal? UnrealizedPnLPct,
    decimal? PurchaseWeight,
    decimal? CurrentWeight,
    decimal? WeightDelta,
    decimal? LastPrice,
    DateTimeOffset? PriceAsOfUtc,
    bool IsPriced,
    bool IsStale);

public sealed record AllocationSliceDto(
    string Key,
    decimal ValueEur,
    decimal Weight);
