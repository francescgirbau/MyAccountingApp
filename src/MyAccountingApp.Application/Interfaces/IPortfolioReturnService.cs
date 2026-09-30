using MyAccountingApp.Contracts;

namespace MyAccountingApp.Application.Interfaces;

/// <summary>
/// Computes the money-weighted (dollar-weighted) return of the portfolio from its dated
/// cash flows and its current market value.
/// </summary>
public interface IPortfolioReturnService
{
    /// <summary>
    /// Calculates the money-weighted return as of the given date.
    /// </summary>
    /// <param name="asOf">The date as of which the return is measured.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The money-weighted return with its supporting figures.</returns>
    Task<PortfolioReturnDto> GetReturnAsync(DateOnly asOf, CancellationToken cancellationToken = default);
}