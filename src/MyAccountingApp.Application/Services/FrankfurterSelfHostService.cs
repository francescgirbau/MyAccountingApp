using System.Globalization;
using MyAccountingApp.Core.DTOs;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Exceptions;
using MyAccountingApp.Domain.Interfaces;

namespace MyAccountingApp.Application.Services;

/// <summary>
/// Serves Frankfurter-compatible v2 rate records from the local conversion cache, so clients
/// can consume rates with no dependency on the public Frankfurter service for availability
/// or latency. Cross-rates for non-EUR bases are derived from the EUR-based cache.
/// </summary>
public class FrankfurterSelfHostService
{
    private readonly IConversionRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="FrankfurterSelfHostService"/> class.
    /// </summary>
    /// <param name="repository">Repository holding the local conversion cache.</param>
    public FrankfurterSelfHostService(IConversionRepository repository)
    {
        this._repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <summary>
    /// Gets the Frankfurter-compatible records for a single date.
    /// </summary>
    /// <param name="date">The date of the rates.</param>
    /// <param name="source">The base currency.</param>
    /// <param name="quotes">Optional restriction to specific target currencies.</param>
    /// <returns>The rate records, one per quote currency.</returns>
    /// <exception cref="ConversionNotAvailableException">Thrown when no conversion is cached for the date.</exception>
    public IReadOnlyList<FrankfurterRateRecord> GetRatesAsync(
        DateOnly date,
        Currencies source,
        IReadOnlyCollection<Currencies>? quotes = null)
    {
        Conversion? conversion = this._repository.GetByDate(date.ToDateTime(TimeOnly.MinValue));

        if (conversion == null)
        {
            throw new ConversionNotAvailableException($"No conversion available for the date {date:yyyy-MM-dd}.");
        }

        return this.ToRecords(date, source, conversion.Quotes, quotes);
    }

    /// <summary>
    /// Gets the Frankfurter-compatible records for a range of dates.
    /// </summary>
    /// <param name="from">The first date of the range (inclusive).</param>
    /// <param name="to">The last date of the range (inclusive).</param>
    /// <param name="source">The base currency.</param>
    /// <param name="quotes">Optional restriction to specific target currencies.</param>
    /// <returns>The rate records, one per quote currency and cached date.</returns>
    /// <exception cref="ConversionNotAvailableException">Thrown when no conversion is cached within the range.</exception>
    public IReadOnlyList<FrankfurterRateRecord> GetRatesAsync(
        DateOnly from,
        DateOnly to,
        Currencies source,
        IReadOnlyCollection<Currencies>? quotes = null)
    {
        List<Conversion> conversions = this._repository.GetAll()
            .Where(c => DateOnly.FromDateTime(c.Date) >= from && DateOnly.FromDateTime(c.Date) <= to)
            .OrderBy(c => c.Date)
            .ToList();

        if (conversions.Count == 0)
        {
            throw new ConversionNotAvailableException($"No conversions available for the range {from:yyyy-MM-dd}..{to:yyyy-MM-dd}.");
        }

        List<FrankfurterRateRecord> records = new();

        foreach (Conversion conversion in conversions)
        {
            records.AddRange(this.ToRecords(DateOnly.FromDateTime(conversion.Date), source, conversion.Quotes, quotes));
        }

        return records;
    }

    /// <summary>
    /// Builds the rate records for one cached conversion, deriving cross-rates when the base differs from EUR.
    /// </summary>
    /// <param name="date">The rate date.</param>
    /// <param name="source">The base currency.</param>
    /// <param name="eurQuotes">Rates expressed as units of each currency per 1 EUR.</param>
    /// <param name="quotes">Optional restriction to specific target currencies.</param>
    /// <returns>The rate records, ordered by quote currency.</returns>
    private IReadOnlyList<FrankfurterRateRecord> ToRecords(
        DateOnly date,
        Currencies source,
        IReadOnlyDictionary<Currencies, decimal> eurQuotes,
        IReadOnlyCollection<Currencies>? quotes)
    {
        List<FrankfurterRateRecord> records = new();
        IEnumerable<Currencies> requested = quotes ?? eurQuotes.Keys;

        foreach (Currencies quote in requested.Where(q => q != source).OrderBy(q => q.ToString(), StringComparer.Ordinal))
        {
            if (!eurQuotes.TryGetValue(quote, out decimal quoteRate))
            {
                continue;
            }

            decimal? rate = source == Currencies.EUR
                ? quoteRate
                : eurQuotes.TryGetValue(source, out decimal baseRate) ? quoteRate / baseRate : null;

            if (rate.HasValue)
            {
                records.Add(new FrankfurterRateRecord
                {
                    Date = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    Base = source.ToString(),
                    Quote = quote.ToString(),
                    Rate = rate.Value,
                });
            }
        }

        return records;
    }
}