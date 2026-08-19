namespace MyAccountingApp.Application.DTOs;

public sealed record DashboardDto(
    DateOnly AsOf,
    CashSnapshotDto Cash,
    PortfolioSnapshotDto Portfolio,
    IReadOnlyList<DashboardAlertDto> Alerts);

public sealed record CashSnapshotDto(
    decimal IncomeMtd,
    decimal ExpenseMtd,
    decimal NetCashFlowMtd,
    decimal IncomeYtd,
    decimal ExpenseYtd,
    decimal NetCashFlowYtd,
    decimal TransfersYtd,
    decimal DepositsYtd,
    decimal FxOutYtd,
    decimal FxInYtd,
    decimal FxNetYtd);

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