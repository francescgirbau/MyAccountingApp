using MyAccountingApp.Contracts;

namespace MyAccountingApp.Application.Interfaces;

public interface IPortfolioQuery
{
    PortfolioPositionDto? GetPosition(string symbol);
}
