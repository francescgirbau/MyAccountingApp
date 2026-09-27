using MyAccountingApp.Contracts;

namespace MyAccountingApp.Application.Interfaces;

public interface IPositionEngine
{
    Task<PortfolioPositionDto?> GetPosition(string symbol, bool includePrice = true);
}
