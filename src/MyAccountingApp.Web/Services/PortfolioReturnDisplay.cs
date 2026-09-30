using MyAccountingApp.Contracts;

namespace MyAccountingApp.Web.Services;

/// <summary>
/// Formats the money-weighted return KPI shown in the Portfolio header.
/// </summary>
public static class PortfolioReturnDisplay
{
    /// <summary>
    /// Gets the rate to show as the main figure: the annualized return when available
    /// (average holding period of at least one year), otherwise the total return.
    /// </summary>
    /// <param name="result">The money-weighted return, or null when unavailable.</param>
    /// <returns>The rate to display, or null when there is nothing to show.</returns>
    public static decimal? MainRate(PortfolioReturnDto? result)
    {
        return result?.AnnualizedReturn ?? result?.TotalReturn;
    }

    /// <summary>
    /// Gets whether the displayed rate is annualized (a per-year figure).
    /// </summary>
    /// <param name="result">The money-weighted return, or null when unavailable.</param>
    /// <returns>True when the rate is annualized.</returns>
    public static bool IsAnnualized(PortfolioReturnDto? result)
    {
        return result?.AnnualizedReturn is not null;
    }

    /// <summary>
    /// Builds the tooltip explaining the calculation and its caveats.
    /// </summary>
    /// <param name="result">The money-weighted return, or null when unavailable.</param>
    /// <returns>The tooltip text.</returns>
    public static string Tooltip(PortfolioReturnDto? result)
    {
        if (result is null || result.TotalReturn is null)
        {
            return "Money-weighted return: no investment flows to measure yet.";
        }

        string annualization = result.AnnualizedReturn is not null
            ? "The average holding period is at least one year, so the return is annualized."
            : "The average holding period is below one year, so the return is not annualized.";

        string tooltip = $"Money-weighted return of the invested capital. Capital invested: {result.CapitalInvestedEur:N2} €; " +
                         $"returns from sales and net dividends: {result.TotalProceedsEur:N2} €; " +
                         $"current market value: {result.TerminalValueEur:N2} €; " +
                         $"average holding period: {result.AyiYears:N1} years. {annualization}";

        if (result.ExcludedFlowCount > 0)
        {
            tooltip += $" {result.ExcludedFlowCount} cash flow(s) with no historical rate were excluded.";
        }

        return tooltip;
    }
}