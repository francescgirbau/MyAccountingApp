using MyAccountingApp.Application.DTOs;

namespace MyAccountingApp.Application.Interfaces;

/// <summary>
/// Computes EUR valuations for all open positions with FX traceability.
/// </summary>
public interface IPositionValuationService
{
    /// <summary>
    /// Computes the EUR valuation of every open position as of the given date.
    /// </summary>
    /// <param name="asOf">The date for which rates are requested.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The valuations, one per symbol with transactions.</returns>
    Task<IReadOnlyList<PositionValuationDto>> GetValuationsAsync(DateOnly asOf, CancellationToken cancellationToken = default);
}
