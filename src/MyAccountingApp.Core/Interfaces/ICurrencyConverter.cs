using MyAccountingApp.Core.Enums;

namespace MyAccountingApp.Core.Interfaces;

/// <summary>
/// Defines a method for fetching currency conversion rates from an external source.
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
    public Task<Dictionary<string, double>> FetchAllRatesAsync(Currencies source, DateTime date);
}
