using MyAccountingApp.Application.DTOs;

namespace MyAccountingApp.Application.Interfaces;

public interface IPortfolioOverviewQuery
{
    Task<PortfolioOverviewDto> GetOverviewAsync(DateOnly asOf, CancellationToken cancellationToken = default);
}
