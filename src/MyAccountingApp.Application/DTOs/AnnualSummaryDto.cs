namespace MyAccountingApp.Application.DTOs;

public record AnnualSummaryDto(
    int Year,
    decimal Expenses,
    decimal Income,
    decimal InvestmentPurchases,
    decimal InvestmentSales,
    decimal NetCashFlow,
    int TransactionCount,
    int AssetTransactionCount);
