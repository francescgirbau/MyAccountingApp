namespace MyAccountingApp.Application.Services;

/// <summary>
/// A single dated cash flow already expressed in EUR. Capital flows (buys) carry a
/// positive amount; proceeds flows (sells and dividends) carry a positive amount, while
/// withholding taxes are negative proceeds.
/// </summary>
public sealed record EurCashFlow(DateOnly Date, decimal AmountEur);

/// <summary>
/// The result of the money-weighted return calculation.
/// </summary>
public sealed record MoneyWeightedReturnResult(
    decimal? TotalReturn,
    decimal? AnnualizedReturn,
    decimal? AyiYears);

/// <summary>
/// Computes the money-weighted (a.k.a. dollar-weighted) return using the methodology of
/// the Simply Wall St portfolio model: the gain (returns plus terminal value minus the
/// invested capital) divided by the invested capital, annualized through the average
/// years invested (AYI) — the capital-weighted average holding period of every buy.
/// </summary>
public static class MoneyWeightedReturnCalculator
{
    private const decimal DaysPerYear = 365.25m;
    private const decimal OneYear = 1m;

    /// <summary>
    /// Computes the money-weighted return.
    /// </summary>
    /// <param name="capitalFlows">Dated capital inputs (buys) in EUR, amounts positive.</param>
    /// <param name="proceedsFlows">Dated returns (sells, dividends) in EUR, amounts positive;
    /// withholding taxes as negative amounts.</param>
    /// <param name="terminalValueEur">Current market value of the portfolio in EUR.</param>
    /// <param name="asOf">The date as of which the return is measured.</param>
    /// <returns>
    /// The total and annualized returns, or nulls when no capital can be measured or when
    /// the loss exceeds the invested capital.
    /// </returns>
    public static MoneyWeightedReturnResult Compute(
        IReadOnlyList<EurCashFlow> capitalFlows,
        IReadOnlyList<EurCashFlow> proceedsFlows,
        decimal terminalValueEur,
        DateOnly asOf)
    {
        decimal capital = capitalFlows.Sum(f => f.AmountEur);
        if (capital <= 0)
        {
            return new MoneyWeightedReturnResult(null, null, null);
        }

        decimal ayi = capitalFlows.Sum(f => f.AmountEur * YearsBetween(f.Date, asOf)) / capital;
        decimal proceeds = proceedsFlows.Sum(f => f.AmountEur);
        decimal totalReturn = (proceeds + terminalValueEur - capital) / capital;

        if (totalReturn <= -OneYear)
        {
            return new MoneyWeightedReturnResult(null, null, ayi);
        }

        decimal? annualized = ayi >= OneYear
            ? (decimal)Math.Pow((double)(OneYear + totalReturn), (double)(OneYear / ayi)) - OneYear
            : null;

        return new MoneyWeightedReturnResult(totalReturn, annualized, ayi);
    }

    private static decimal YearsBetween(DateOnly start, DateOnly end)
    {
        int days = end.DayNumber - start.DayNumber;
        return days <= 0 ? 0m : days / DaysPerYear;
    }
}