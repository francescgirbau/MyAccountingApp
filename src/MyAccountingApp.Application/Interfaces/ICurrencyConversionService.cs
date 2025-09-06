using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.ValueObjects;

namespace MyAccountingApp.Application.Interfaces;

/// <summary>
/// Defines a service for converting monetary values between currencies.
/// </summary>
public interface ICurrencyConversionService
{
    /// <summary>
    /// Asynchronously converts a monetary value to the specified target currency using the exchange rate for the given date.
    /// </summary>
    /// <param name="original">The original monetary value to convert.</param>
    /// <param name="targetCurrency">The currency to convert to.</param>
    /// <param name="date">The date for which to use the exchange rate.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the converted monetary value.
    /// </returns>
    Task<Money> ConvertToAsync(Money original, Currencies targetCurrency, DateTime date);
}
