using MyAccountingApp.Core.Enums;

namespace MyAccountingApp.Core.Entities;

/// <summary>
/// Represents a currency conversion for a specific date and base currency.
/// </summary>
public class Conversion
{
    /// <summary>
    /// Gets the date of the conversion.
    /// </summary>
    public DateTime Date { get; }

    /// <summary>
    /// Gets the base currency for the conversion.
    /// </summary>
    public Currencies Source { get; }

    /// <summary>
    /// Gets the dictionary of target currencies and their conversion rates.
    /// </summary>
    public Dictionary<Currencies, double> Quotes { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Conversion"/> class.
    /// </summary>
    /// <param name="date">The date of the conversion.</param>
    /// <param name="source">The base currency.</param>
    /// <param name="quotes">Optional dictionary of conversion quotes.</param>
    /// <exception cref="ArgumentException">Thrown if the source currency is not EUR.</exception>
    public Conversion(DateTime date, Currencies source, Dictionary<Currencies, double>? quotes = null)
    {
        this.Date = date.Date;
        this.Source = source;
        this.Quotes = quotes ?? new Dictionary<Currencies, double>();

        this.Validate();
    }

    /// <summary>
    /// Validates the base currency. Only EUR is supported.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown if the base currency is not EUR.</exception>
    private void Validate()
    {
        if (this.Source != Currencies.EUR)
        {
            throw new ArgumentException(
                $"The source currency must be EUR. Provided: {this.Source}",
                nameof(this.Source));
        }
    }

    /// <summary>
    /// Adds or updates a conversion quote for a target currency.
    /// </summary>
    /// <param name="target">The target currency.</param>
    /// <param name="rate">The conversion rate.</param>
    /// <exception cref="InvalidOperationException">Thrown if the target currency is the same as the source currency.</exception>
    public void AddOrUpdateQuote(Currencies target, double rate)
    {
        if (target == this.Source)
        {
            throw new InvalidOperationException("Target currency cannot be the same as source currency.");
        }

        this.Quotes[target] = rate;
    }

    /// <summary>
    /// Tries to get the conversion rate for a target currency.
    /// </summary>
    /// <param name="target">The target currency.</param>
    /// <param name="rate">The conversion rate, if found.</param>
    /// <returns>True if the quote exists; otherwise, false.</returns>
    public bool TryGetQuote(Currencies target, out double rate)
    {
        return this.Quotes.TryGetValue(target, out rate);
    }

    /// <summary>
    /// Determines whether the conversion matches the specified date.
    /// </summary>
    /// <param name="date">The date to compare.</param>
    /// <returns>True if the dates match; otherwise, false.</returns>
    public bool MatchesDate(DateTime date)
    {
        return this.Date.Date == date.Date;
    }
}
