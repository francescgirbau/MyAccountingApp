using MyAccountingApp.Domain.Enums;

namespace MyAccountingApp.Application.Interfaces;

/// <summary>
/// Defines a service for retrieving currency conversion rates for a specific date.
/// </summary>
public interface ICurrencyRateService
{
    /// <summary>
    /// Asynchronously retrieves conversion rates for all supported currencies based on the specified date.
    /// </summary>
    /// <param name="date">The date for which to retrieve currency conversion rates.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains a dictionary
    /// mapping currencies to their conversion rates.
    /// </returns>
    Task<Dictionary<Currencies, double>> GetQuotes(DateTime date);
}
