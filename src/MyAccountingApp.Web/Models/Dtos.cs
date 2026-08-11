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
}

public class AssetTransactionDto
{
    public TransactionDto Transaction { get; set; } = new();
    public string Symbol { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Type { get; set; } = string.Empty;
    public MoneyDto UnitaryCost { get; set; } = new();
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

public class ValidationError
{
    public string Field { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
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

public class DashboardDto
{
    public DateOnly AsOf { get; set; }
    public CashSnapshotDto Cash { get; set; } = new();
    public PortfolioSnapshotDto Portfolio { get; set; } = new();
    public List<DashboardAlertDto> Alerts { get; set; } = new();
}

public class CashSnapshotDto
{
    public decimal IncomeMtd { get; set; }
    public decimal ExpenseMtd { get; set; }
    public decimal NetCashFlowMtd { get; set; }
    public decimal IncomeYtd { get; set; }
    public decimal ExpenseYtd { get; set; }
    public decimal NetCashFlowYtd { get; set; }
    public decimal TransfersYtd { get; set; }
    public decimal DepositsYtd { get; set; }
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
