namespace MyAccountingApp.Application.DTOs;

public record MonthlySummaryDto(
    int Month,
    decimal Expenses,
    decimal Income,
    decimal InvestmentPurchases,
    decimal InvestmentSales,
    decimal NetCashFlow,
    int TransactionCount,
    int AssetTransactionCount);

public record AnnualSummaryDto(
    int Year,
    decimal Expenses,
    decimal Income,
    decimal InvestmentPurchases,
    decimal InvestmentSales,
    decimal NetCashFlow,
    int TransactionCount,
    int AssetTransactionCount,
    List<MonthlySummaryDto> Months);
