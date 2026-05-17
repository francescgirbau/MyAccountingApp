using MyAccountingApp.Application.DTOs;

namespace MyAccountingApp.Application.Interfaces;

public interface IPortfolioQuery
{
    PortfolioPositionDto? GetPosition(string symbol);
}
