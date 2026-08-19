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
    decimal Deposits,
    decimal FxOut,
    decimal FxIn,
    decimal FxNet);

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
    bool IncludesAssetCashFlows,
    decimal FxOut,
    decimal FxIn,
    decimal FxNet,
    int FxPairCount,
    int FxUnmatchedLegCount);
