using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Exceptions;

namespace MyAccountingApp.Application.Services;

/// <summary>
/// Derives cross-currency rates from the EUR-based quotes stored in the conversion cache.
/// The cache stores rates as units of each currency per 1 EUR, so any pair can be
/// derived arithmetically: base→quote = rate(quote) / rate(base), with EUR on either
/// side simplifying to the stored rate or its inverse.
/// </summary>
public static class FxRateDeriver
{
    /// <summary>
    /// Derives the rate of a single pair (units of target per 1 unit of source) from EUR-based quotes.
    /// </summary>
    /// <param name="source">The base currency of the pair.</param>
    /// <param name="target">The quote currency of the pair.</param>
    /// <param name="eurQuotes">Rates expressed as units of each currency per 1 EUR.</param>
    /// <returns>The derived rate, or 1 when source equals target.</returns>
    /// <exception cref="ConversionNotAvailableException">Thrown when a required quote is missing.</exception>
    public static decimal Derive(Currencies source, Currencies target, IReadOnlyDictionary<Currencies, decimal> eurQuotes)
    {
        if (source == target)
        {
            return 1m;
        }

        if (source == Currencies.EUR)
        {
            return RequireQuote(target, eurQuotes);
        }

        if (target == Currencies.EUR)
        {
            return 1m / RequireQuote(source, eurQuotes);
        }

        return RequireQuote(target, eurQuotes) / RequireQuote(source, eurQuotes);
    }

    /// <summary>
    /// Derives a full set of quotes for a base currency from EUR-based quotes, excluding the base itself.
    /// </summary>
    /// <param name="source">The base currency of the derived conversion.</param>
    /// <param name="eurQuotes">Rates expressed as units of each currency per 1 EUR.</param>
    /// <returns>The derived quotes (units of each target per 1 unit of source).</returns>
    /// <exception cref="ConversionNotAvailableException">Thrown when a required quote is missing.</exception>
    public static IReadOnlyDictionary<Currencies, decimal> DeriveAll(
        Currencies source,
        IReadOnlyDictionary<Currencies, decimal> eurQuotes)
    {
        Dictionary<Currencies, decimal> result = new();

        foreach (Currencies target in eurQuotes.Keys)
        {
            if (target == source)
            {
                continue;
            }

            result[target] = Derive(source, target, eurQuotes);
        }

        return result;
    }

    private static decimal RequireQuote(Currencies currency, IReadOnlyDictionary<Currencies, decimal> eurQuotes)
    {
        if (eurQuotes.TryGetValue(currency, out decimal quote))
        {
            return quote;
        }

        throw new ConversionNotAvailableException($"No EUR-based quote available for {currency} to derive a cross-rate.");
    }
}