using MyAccountingApp.Application.DTOs;

namespace MyAccountingApp.Application.Interfaces;

public interface IPositionEngine
{
    PortfolioPositionDto? GetPosition(string symbol);
}
