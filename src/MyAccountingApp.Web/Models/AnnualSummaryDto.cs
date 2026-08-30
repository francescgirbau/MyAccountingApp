namespace MyAccountingApp.Web.Models;

public class RawCsvResultDto
{
    public int Imported { get; set; }
    public int Skipped { get; set; }
    public List<string>? Errors { get; set; }
}

public class MonthlyOperatingDto
{
    public decimal Income { get; set; }
    public decimal Expenses { get; set; }
    public decimal Dividends { get; set; }
    public decimal InterestIncome { get; set; }
    public decimal Fees { get; set; }
    public decimal WithholdingTax { get; set; }
    public decimal NetOperatingCashFlow { get; set; }
}

public class MonthlyInvestingDto
{
    public decimal Purchases { get; set; }
    public decimal Sales { get; set; }
    public decimal NetInvestedCash { get; set; }
}

public class MonthlyInternalDto
{
    public decimal Transfers { get; set; }
    public decimal Deposits { get; set; }
    public decimal FxOut { get; set; }
    public decimal FxIn { get; set; }
    public decimal FxNet { get; set; }
    public int FxPairCount { get; set; }
    public int FxUnmatchedLegCount { get; set; }
}

public class MonthlySummaryDto
{
    public int Month { get; set; }
    public MonthlyOperatingDto Operating { get; set; } = new();
    public MonthlyInvestingDto Investing { get; set; } = new();
    public MonthlyInternalDto Internal { get; set; } = new();
    public int TransactionCount { get; set; }
    public int AssetTransactionCount { get; set; }
}

public class AnnualOperatingDto
{
    public decimal Income { get; set; }
    public decimal Expenses { get; set; }
    public decimal Dividends { get; set; }
    public decimal InterestIncome { get; set; }
    public decimal Fees { get; set; }
    public decimal WithholdingTax { get; set; }
    public decimal NetOperatingCashFlow { get; set; }
}

public class AnnualInvestingDto
{
    public decimal Purchases { get; set; }
    public decimal Sales { get; set; }
    public decimal NetInvestedCash { get; set; }
}

public class AnnualInternalDto
{
    public decimal Transfers { get; set; }
    public decimal Deposits { get; set; }
    public decimal FxOut { get; set; }
    public decimal FxIn { get; set; }
    public decimal FxNet { get; set; }
    public int FxPairCount { get; set; }
    public int FxUnmatchedLegCount { get; set; }
}

public class AnnualSummaryDto
{
    public int Year { get; set; }
    public AnnualOperatingDto Operating { get; set; } = new();
    public AnnualInvestingDto Investing { get; set; } = new();
    public AnnualInternalDto Internal { get; set; } = new();
    public List<MonthlySummaryDto> Months { get; set; } = new();
    public bool IncludesAssetCashFlows { get; set; }
    public int TransactionCount { get; set; }
    public int AssetTransactionCount { get; set; }
}
