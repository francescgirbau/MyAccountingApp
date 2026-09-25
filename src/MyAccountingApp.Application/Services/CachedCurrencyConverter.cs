using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Exceptions;
using MyAccountingApp.Domain.Interfaces;

namespace MyAccountingApp.Application.Services;

/// <summary>
/// Cache-first currency provider that serves rates exclusively from the local conversion
/// cache, deriving cross-rates for any base currency. When a date is missing from the cache
/// an optional fallback converter (e.g. the public Frankfurter service) can be consulted so
/// the cache can be seeded or enlarged, keeping every read independent of the public service
/// for availability and latency.
/// </summary>
public class CachedCurrencyConverter : ICurrencyConverter
{
    private readonly IConversionRepository _repository;
    private readonly ICurrencyConverter? _fallback;
    private readonly IReadOnlyCollection<string> _excludedCurrencies;

    /// <summary>
    /// Initializes a new instance of the <see cref="CachedCurrencyConverter"/> class.
    /// </summary>
    /// <param name="repository">Repository holding the local conversion cache.</param>
    /// <param name="fallback">Optional provider consulted when the cache cannot serve a request.</param>
    /// <param name="excludedCurrencies">Optional list of currency codes to skip (e.g. "BTC").</param>
    public CachedCurrencyConverter(
        IConversionRepository repository,
        ICurrencyConverter? fallback = null,
        IReadOnlyCollection<string>? excludedCurrencies = null)
    {
        this._repository = repository ?? throw new ArgumentNullException(nameof(repository));
        this._fallback = fallback;
        this._excludedCurrencies = excludedCurrencies ?? Array.Empty<string>();
    }

    /// <inheritdoc/>
    public async Task<Dictionary<string, decimal>> FetchAllRatesAsync(Currencies source, DateTime date)
    {
        Conversion? cached = this._repository.GetByDate(date);

        if (cached != null && cached.Quotes.Count > 0)
        {
            return this.ToPairRates(source, cached.Quotes);
        }

        if (this._fallback != null)
        {
            return await this._fallback.FetchAllRatesAsync(source, date);
        }

        throw new ConversionNotAvailableException($"No cached conversion available for {date:yyyy-MM-dd} to serve the request.");
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<DateOnly, Dictionary<string, decimal>>> FetchRangeAsync(
        Currencies source,
        DateOnly start,
        DateOnly end,
        IReadOnlyCollection<Currencies>? targets = null,
        CancellationToken cancellationToken = default)
    {
        List<Conversion> cachedDays = this._repository.GetAll()
            .Where(c => DateOnly.FromDateTime(c.Date) >= start && DateOnly.FromDateTime(c.Date) <= end)
            .OrderBy(c => c.Date)
            .ToList();

        Dictionary<DateOnly, Dictionary<string, decimal>> result = new();

        foreach (Conversion conversion in cachedDays)
        {
            result[DateOnly.FromDateTime(conversion.Date)] = this.ToPairRates(source, conversion.Quotes, targets);
        }

        if (this._fallback != null && result.Count < ((end.DayNumber - start.DayNumber) + 1))
        {
            IReadOnlyDictionary<DateOnly, Dictionary<string, decimal>> fallbackRates = await this._fallback.FetchRangeAsync(source, start, end, targets, cancellationToken);

            foreach (KeyValuePair<DateOnly, Dictionary<string, decimal>> day in fallbackRates)
            {
                if (!result.ContainsKey(day.Key))
                {
                    result[day.Key] = day.Value;
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Builds the pair-code to rate dictionary for a source currency from EUR-based quotes,
    /// skipping targets that are missing from the cache or are excluded.
    /// </summary>
    /// <param name="source">The base currency of the pairs.</param>
    /// <param name="eurQuotes">Rates expressed as units of each currency per 1 EUR.</param>
    /// <param name="targets">Optional restriction to specific target currencies.</param>
    /// <returns>The pair-code to rate dictionary.</returns>
    private Dictionary<string, decimal> ToPairRates(
        Currencies source,
        IReadOnlyDictionary<Currencies, decimal> eurQuotes,
        IReadOnlyCollection<Currencies>? targets = null)
    {
        Dictionary<string, decimal> result = new(StringComparer.Ordinal);
        IEnumerable<Currencies> requested = targets ?? eurQuotes.Keys;

        foreach (Currencies quote in requested)
        {
            if (quote == source || !this.IsRequested(quote))
            {
                continue;
            }

            if (!eurQuotes.TryGetValue(quote, out decimal quoteRate))
            {
                continue;
            }

            if (source == Currencies.EUR)
            {
                result[$"{source}{quote}"] = quoteRate;
            }
            else if (eurQuotes.TryGetValue(source, out decimal baseRate))
            {
                result[$"{source}{quote}"] = quoteRate / baseRate;
            }
        }

        return result;
    }

    /// <summary>
    /// Determines whether a currency is not excluded from requests.
    /// </summary>
    /// <param name="currency">The currency to check.</param>
    /// <returns>True when the currency should be requested; otherwise, false.</returns>
    private bool IsRequested(Currencies currency)
    {
        return !this._excludedCurrencies.Contains(currency.ToString(), StringComparer.OrdinalIgnoreCase);
    }
}