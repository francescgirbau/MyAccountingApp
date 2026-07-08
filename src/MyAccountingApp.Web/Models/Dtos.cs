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
