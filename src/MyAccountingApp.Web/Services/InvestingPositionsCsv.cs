namespace MyAccountingApp.Web.Services;
using System.Globalization;
using System.Text;
using MyAccountingApp.Contracts;

/// <summary>
/// Builds the CSV document accepted by the Investing.com holdings import: one row per open
/// position with its symbol, average open price, absolute amount, direction and currency.
/// </summary>
public static class InvestingPositionsCsv
{
    /// <summary>
    /// Renders the given positions as an Investing.com compatible CSV. Fully closed positions
    /// are skipped, prices and amounts are exported as absolute values and the direction column
    /// carries the sign as BUY for long positions and SELL for short ones.
    /// </summary>
    public static string Build(IReadOnlyList<PortfolioPositionDto> positions)
    {
        StringBuilder csv = new();
        csv.AppendLine("Symbol,Open Price,Amount,Type,Currency");

        foreach (PortfolioPositionDto position in positions)
        {
            if (position.NetQuantity == 0)
            {
                continue;
            }

            decimal openPrice = Math.Round(Math.Abs(position.AverageUnitaryCost), 4);
            decimal amount = Math.Abs(position.NetQuantity);
            string direction = position.NetQuantity > 0 ? "BUY" : "SELL";

            csv.Append(Escape(position.Symbol))
                .Append(',')
                .Append(openPrice.ToString(CultureInfo.InvariantCulture))
                .Append(',')
                .Append(amount.ToString(CultureInfo.InvariantCulture))
                .Append(',')
                .Append(direction)
                .Append(',')
                .Append(Escape(position.Currency))
                .AppendLine();
        }

        return csv.ToString();
    }

    /// <summary>
    /// Quotes a CSV field when it contains a comma, a double quote or a line break.
    /// </summary>
    private static string Escape(string field)
    {
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
        {
            return "\"" + field.Replace("\"", "\"\"") + "\"";
        }

        return field;
    }
}
