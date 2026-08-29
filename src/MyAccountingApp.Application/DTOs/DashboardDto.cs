namespace MyAccountingApp.Application.DTOs;

public sealed record DashboardDto(
    DateOnly AsOf,
    CashSnapshotDto Cash,
    PortfolioSnapshotDto Portfolio,
    IReadOnlyList<DashboardAlertDto> Alerts);

public sealed record CashSnapshotDto(
    OperatingCashFlowDto OperatingMtd,
    OperatingCashFlowDto OperatingYtd,
    InvestingCashFlowDto InvestingYtd,
    InternalCashFlowDto InternalYtd);

public sealed record OperatingCashFlowDto(
    decimal Income,
    decimal Expenses,
    decimal NetOperatingCashFlow);

public sealed record InvestingCashFlowDto(
    decimal Purchases,
    decimal Sales,
    decimal NetInvestedCash);

public sealed record InternalCashFlowDto(
    decimal Transfers,
    decimal Deposits,
    decimal FxOut,
    decimal FxIn,
    decimal FxNet);

public sealed record PortfolioSnapshotDto(
    decimal TotalCostBasisEur,
    decimal? TotalMarketValueEur,
    decimal? TotalUnrealizedEur,
    decimal RealizedGainLossYtdEur,
    int OpenPositionCount,
    int SymbolCount,
    int SymbolsWithoutPrice);

public sealed record DashboardAlertDto(
    string Severity,
    string Code,
    string Message,
    string? Link);