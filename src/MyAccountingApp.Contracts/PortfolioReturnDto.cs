namespace MyAccountingApp.Contracts;

/// <summary>
/// Money-weighted (dollar-weighted) portfolio return, based on the
/// "average years invested" (AYI) methodology: the annualized return is only
/// meaningful once the average holding period reaches one year.
/// </summary>
public sealed record PortfolioReturnDto(
    decimal? TotalReturn,
    decimal? AnnualizedReturn,
    decimal? AyiYears,
    decimal CapitalInvestedEur,
    decimal TotalProceedsEur,
    decimal TerminalValueEur,
    int ExcludedFlowCount);