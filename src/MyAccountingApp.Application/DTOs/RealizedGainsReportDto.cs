namespace MyAccountingApp.Application.DTOs;

public record RealizedGainsReportDto(
    int Year,
    decimal TotalRealizedGainLoss,
    List<SymbolRealizedGainsDto> Symbols);

public record SymbolRealizedGainsDto(
    string Symbol,
    string Currency,
    decimal SoldQuantity,
    decimal Proceeds,
    decimal CostBasis,
    decimal RealizedGainLoss,
    List<RealizedSaleDto> Sales);

public record RealizedSaleDto(
    DateTime Date,
    decimal Quantity,
    decimal Proceeds,
    decimal CostBasis,
    decimal RealizedGainLoss);

public record WithholdingReportDto(
    int Year,
    List<WithholdingTotalDto> Totals);

public record WithholdingTotalDto(
    string Currency,
    decimal Amount,
    int TransactionCount);
