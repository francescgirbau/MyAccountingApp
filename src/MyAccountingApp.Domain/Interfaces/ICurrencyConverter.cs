using MyAccountingApp.Domain.Enums;

namespace MyAccountingApp.Domain.Interfaces;

/// <summary>
/// Defines methods for fetching currency conversion rates from an external source.
/// </summary>
public interface ICurrencyConverter
{
    /// <summary>
    /// Asynchronously fetches conversion rates for all supported currencies based on the specified source currency and date.
    /// </summary>
    /// <param name="source">The base currency for conversion.</param>
    /// <param name="date">The date for which to fetch conversion rates.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains a dictionary
    /// mapping currency pair codes (e.g., "EURUSD") to their conversion rates.
    /// </returns>
    Task<Dictionary<string, decimal>> FetchAllRatesAsync(Currencies source, DateTime date);

    /// <summary>
    /// Asynchronously fetches conversion rates for a range of dates in a single request.
    /// </summary>
    /// <param name="source">The base currency for conversion.</param>
    /// <param name="start">The first date of the range (inclusive).</param>
    /// <param name="end">The last date of the range (inclusive).</param>
    /// <param name="targets">Optional list of target currencies; defaults to all supported currencies except the source.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains a dictionary
    /// mapping each date to a dictionary of currency pair codes (e.g., "EURUSD") to rates.
    /// </returns>
    Task<IReadOnlyDictionary<DateOnly, Dictionary<string, decimal>>> FetchRangeAsync(
        Currencies source,
        DateOnly start,
        DateOnly end,
        IReadOnlyCollection<Currencies>? targets = null,
        CancellationToken cancellationToken = default);
}
