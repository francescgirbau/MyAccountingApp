namespace MyAccountingApp.Application.DTOs;

public record MonthlySummaryDto(
    int Month,
    decimal Expenses,
    decimal Income,
    decimal InvestmentPurchases,
    decimal InvestmentSales,
    decimal NetCashFlow,
    int TransactionCount,
    int AssetTransactionCount,
    decimal Transfers,
    decimal Deposits);

public record AnnualSummaryDto(
    int Year,
    decimal Expenses,
    decimal Income,
    decimal InvestmentPurchases,
    decimal InvestmentSales,
    decimal NetCashFlow,
    int TransactionCount,
    int AssetTransactionCount,
    List<MonthlySummaryDto> Months,
    decimal Transfers,
    decimal Deposits,
    bool IncludesAssetCashFlows);
