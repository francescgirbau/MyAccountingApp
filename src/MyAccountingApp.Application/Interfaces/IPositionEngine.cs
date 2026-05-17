using MyAccountingApp.Application.DTOs;

namespace MyAccountingApp.Application.Interfaces;

public interface IPositionEngine
{
    Task<PortfolioPositionDto?> GetPosition(string symbol);
}
