namespace MyAccountingApp.Web.Models;

public class MonthlySummaryDto
{
    public int Month { get; set; }
    public decimal Expenses { get; set; }
    public decimal Income { get; set; }
    public decimal InvestmentPurchases { get; set; }
    public decimal InvestmentSales { get; set; }
    public decimal NetCashFlow { get; set; }
    public int TransactionCount { get; set; }
    public int AssetTransactionCount { get; set; }
}

public class AnnualSummaryDto
{
    public int Year { get; set; }
    public decimal Expenses { get; set; }
    public decimal Income { get; set; }
    public decimal InvestmentPurchases { get; set; }
    public decimal InvestmentSales { get; set; }
    public decimal NetCashFlow { get; set; }
    public int TransactionCount { get; set; }
    public int AssetTransactionCount { get; set; }
    public List<MonthlySummaryDto>? Months { get; set; }
}
