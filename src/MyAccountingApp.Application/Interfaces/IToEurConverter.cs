using MyAccountingApp.Application.DTOs;
using MyAccountingApp.Domain.ValueObjects;

namespace MyAccountingApp.Application.Interfaces;

/// <summary>
/// Converts a monetary amount to EUR using the currency rate service, keeping the
/// applied rate date traceable when the quote is stale.
/// </summary>
public interface IToEurConverter
{
    /// <summary>
    /// Converts the given money to EUR using the rate for the specified date.
    /// </summary>
    /// <param name="money">The amount to convert. EUR amounts are returned unchanged.</param>
    /// <param name="date">The date for which the rate is requested.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The EUR amount with the applied rate and its actual rate date.</returns>
    Task<EurConversionDto> ToEurAsync(Money money, DateOnly date, CancellationToken cancellationToken = default);
}
