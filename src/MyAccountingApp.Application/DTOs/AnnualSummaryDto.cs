namespace MyAccountingApp.Application.DTOs;

public record MonthlyOperatingDto(
    decimal Income,
    decimal Expenses,
    decimal Dividends,
    decimal InterestIncome,
    decimal Fees,
    decimal WithholdingTax,
    decimal NetOperatingCashFlow);

public record MonthlyInvestingDto(
    decimal Purchases,
    decimal Sales,
    decimal NetInvestedCash);

public record MonthlyInternalDto(
    decimal Transfers,
    decimal Deposits,
    decimal FxOut,
    decimal FxIn,
    decimal FxNet,
    int FxPairCount,
    int FxUnmatchedLegCount);

public record MonthlySummaryDto(
    int Month,
    MonthlyOperatingDto Operating,
    MonthlyInvestingDto Investing,
    MonthlyInternalDto Internal,
    int TransactionCount,
    int AssetTransactionCount);

public record AnnualOperatingDto(
    decimal Income,
    decimal Expenses,
    decimal Dividends,
    decimal InterestIncome,
    decimal Fees,
    decimal WithholdingTax,
    decimal NetOperatingCashFlow);

public record AnnualInvestingDto(
    decimal Purchases,
    decimal Sales,
    decimal NetInvestedCash);

public record AnnualInternalDto(
    decimal Transfers,
    decimal Deposits,
    decimal FxOut,
    decimal FxIn,
    decimal FxNet,
    int FxPairCount,
    int FxUnmatchedLegCount);

public record AnnualSummaryDto(
    int Year,
    AnnualOperatingDto Operating,
    AnnualInvestingDto Investing,
    AnnualInternalDto Internal,
    List<MonthlySummaryDto> Months,
    bool IncludesAssetCashFlows,
    int TransactionCount,
    int AssetTransactionCount);
