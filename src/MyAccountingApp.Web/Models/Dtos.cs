namespace MyAccountingApp.Web.Models;

public class MoneyDto
{
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
}

public class TransactionDto
{
    public Guid Id { get; set; }
    public DateTime Date { get; set; }
    public string Description { get; set; } = string.Empty;
    public MoneyDto Money { get; set; } = new();
    public string Category { get; set; } = string.Empty;
    public string? Source { get; set; }
    public Guid? FxPairId { get; set; }
    public string? FxLeg { get; set; }
    public decimal? FxBrokerRate { get; set; }
    public string? FxExternalKey { get; set; }
}

public class AssetTransactionDto
{
    public TransactionDto Transaction { get; set; } = new();
    public string Symbol { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Type { get; set; } = string.Empty;
    public MoneyDto UnitaryCost { get; set; } = new();
    public string? Source { get; set; }
}

public class OptionTransactionDto
{
    public TransactionDto Transaction { get; set; } = new();
    public string Symbol { get; set; } = string.Empty;
    public string Isin { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Type { get; set; } = string.Empty;
}

public class PortfolioPositionDto
{
    public string Symbol { get; set; } = string.Empty;
    public decimal NetQuantity { get; set; }
    public decimal AverageUnitaryCost { get; set; }
    public decimal TotalCostBasis { get; set; }
    public string Currency { get; set; } = string.Empty;
    public int TransactionCount { get; set; }
    public decimal RealizedGainLoss { get; set; }
    public decimal? MarketPrice { get; set; }
    public decimal? UnrealizedGainLoss { get; set; }
    public bool HasShortfall { get; set; }
    public decimal UnmatchedSellQuantity { get; set; }
    public List<TaxLotDto> OpenLots { get; set; } = new();
}

public class TaxLotDto
{
    public DateTime PurchaseDate { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitaryCost { get; set; }
    public decimal TotalCost { get; set; }
}

public class ImportResultDto
{
    public List<TransactionDto> Transactions { get; set; } = new();
    public List<AssetTransactionDto> AssetTransactions { get; set; } = new();
    public List<OptionTransactionDto> OptionTransactions { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public List<ValidationError> ValidationErrors { get; set; } = new();
    public List<ValidationError> ValidationWarnings { get; set; } = new();
    public int FilesProcessed { get; set; }
}

public class PositionValuationDto
{
    public string Symbol { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public decimal NetQuantity { get; set; }
    public decimal? MarketPrice { get; set; }
    public decimal? UnrealizedGainLoss { get; set; }
    public decimal? ValueEur { get; set; }
    public decimal? UnrealizedGainLossEur { get; set; }
    public decimal? Rate { get; set; }
    public DateOnly? RateDate { get; set; }
    public bool IsStale { get; set; }
    public string? Provider { get; set; }
}

public class PositionValuationResponse
{
    public DateOnly AsOf { get; set; }
    public List<PositionValuationDto> Positions { get; set; } = new();
}

public class ValidationError
{
    public string Field { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string EntityType { get; set; } = "Transaction";
    public List<Guid> EntityIds { get; set; } = new();
    public string? Symbol { get; set; }
    public string? DeepLink { get; set; }
}

public class ValidationResponseDto
{
    public bool IsValid { get; set; }
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
    public List<ValidationError> Errors { get; set; } = new();
    public List<ValidationError> Warnings { get; set; } = new();
}

public class TransferMatchingResultDto
{
    public int TransferCount { get; set; }
    public int MatchedPairs { get; set; }
    public int UnmatchedTransfers { get; set; }
    public int ChangedTransactions { get; set; }
    public DateTime CalculatedAtUtc { get; set; }
}

public class BatchPatchFailureDto
{
    public Guid Id { get; set; }
    public string Error { get; set; } = string.Empty;
}

public class BatchPatchResultDto
{
    public int Requested { get; set; }
    public int Updated { get; set; }
    public List<BatchPatchFailureDto> Failures { get; set; } = new();
}

public class BulkDeleteResultDto
{
    public int Requested { get; set; }
    public int Deleted { get; set; }
    public List<BatchPatchFailureDto> Failures { get; set; } = new();
}

public class BulkDeleteRequest
{
    public List<Guid> Ids { get; set; } = new();
}

public class DashboardDto
{
    public DateOnly AsOf { get; set; }
    public CashSnapshotDto Cash { get; set; } = new();
    public PortfolioSnapshotDto Portfolio { get; set; } = new();
    public List<DashboardAlertDto> Alerts { get; set; } = new();
}

public class CashSnapshotDto
{
    public OperatingCashFlowDto OperatingMtd { get; set; } = new();
    public OperatingCashFlowDto OperatingYtd { get; set; } = new();
    public InvestingCashFlowDto InvestingYtd { get; set; } = new();
    public InternalCashFlowDto InternalYtd { get; set; } = new();
}

public class OperatingCashFlowDto
{
    public decimal Income { get; set; }
    public decimal Expenses { get; set; }
    public decimal NetOperatingCashFlow { get; set; }
}

public class InvestingCashFlowDto
{
    public decimal Purchases { get; set; }
    public decimal Sales { get; set; }
    public decimal NetInvestedCash { get; set; }
}

public class InternalCashFlowDto
{
    public decimal Transfers { get; set; }
    public decimal Deposits { get; set; }
    public decimal FxOut { get; set; }
    public decimal FxIn { get; set; }
    public decimal FxNet { get; set; }
}

public class PortfolioSnapshotDto
{
    public decimal TotalCostBasisEur { get; set; }
    public decimal? TotalMarketValueEur { get; set; }
    public decimal? TotalUnrealizedEur { get; set; }
    public decimal RealizedGainLossYtdEur { get; set; }
    public int OpenPositionCount { get; set; }
    public int SymbolCount { get; set; }
    public int SymbolsWithoutPrice { get; set; }
}

public class DashboardAlertDto
{
    public string Severity { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Link { get; set; }
}

public class RealizedGainsReportDto
{
    public int Year { get; set; }
    public decimal TotalRealizedGainLoss { get; set; }
    public List<SymbolRealizedGainsDto> Symbols { get; set; } = new();
}

public class SymbolRealizedGainsDto
{
    public string Symbol { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public decimal SoldQuantity { get; set; }
    public decimal Proceeds { get; set; }
    public decimal CostBasis { get; set; }
    public decimal RealizedGainLoss { get; set; }
    public List<RealizedSaleDto> Sales { get; set; } = new();
}

public class RealizedSaleDto
{
    public DateTime Date { get; set; }
    public decimal Quantity { get; set; }
    public decimal Proceeds { get; set; }
    public decimal CostBasis { get; set; }
    public decimal RealizedGainLoss { get; set; }
}

public class WithholdingReportDto
{
    public int Year { get; set; }
    public List<WithholdingTotalDto> Totals { get; set; } = new();
}

public class WithholdingTotalDto
{
    public string Currency { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int TransactionCount { get; set; }
}

public class AuthStatusDto
{
    public bool IsEnabled { get; set; }
    public bool IsInitialized { get; set; }
    public bool IsUnlocked { get; set; }
}

public class PortfolioOverviewDto
{
    public decimal MarketValueEur { get; set; }
    public decimal InvestedCostEur { get; set; }
    public decimal UnrealizedPnLEur { get; set; }
    public decimal? UnrealizedPnLPct { get; set; }
    public DateTimeOffset? PricesAsOfUtc { get; set; }
    public bool IsMarketClosed { get; set; }
    public int UnpricedPositionCount { get; set; }
    public int OptionSymbolCount { get; set; }
    public List<PortfolioPositionRowDto> Positions { get; set; } = new();
    public List<AllocationSliceDto> PurchaseAllocation { get; set; } = new();
    public List<AllocationSliceDto> CurrentAllocation { get; set; } = new();
}

public class PortfolioPositionRowDto
{
    public string Symbol { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal Cost { get; set; }
    public string Currency { get; set; } = string.Empty;
    public decimal? CostEur { get; set; }
    public decimal? MarketValue { get; set; }
    public decimal? MarketValueEur { get; set; }
    public decimal? UnrealizedPnL { get; set; }
    public decimal? UnrealizedPnLPct { get; set; }
    public decimal? PurchaseWeight { get; set; }
    public decimal? CurrentWeight { get; set; }
    public decimal? WeightDelta { get; set; }
    public decimal? LastPrice { get; set; }
    public DateTimeOffset? PriceAsOfUtc { get; set; }
    public bool IsPriced { get; set; }
    public bool IsStale { get; set; }
}

public class AllocationSliceDto
{
    public string Key { get; set; } = string.Empty;
    public decimal ValueEur { get; set; }
    public decimal Weight { get; set; }
}
