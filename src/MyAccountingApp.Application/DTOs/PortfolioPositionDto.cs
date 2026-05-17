namespace MyAccountingApp.Application.DTOs;

public record PortfolioPositionDto(
    string Symbol,
    decimal NetQuantity,
    decimal AverageUnitaryCost,
    decimal TotalCostBasis,
    string Currency,
    int TransactionCount,
    decimal RealizedGainLoss,
    List<TaxLotDto> OpenLots,
    decimal? MarketPrice,
    decimal? UnrealizedGainLoss);
