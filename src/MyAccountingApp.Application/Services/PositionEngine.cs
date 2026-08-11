using MyAccountingApp.Application.DTOs;
using MyAccountingApp.Application.Interfaces;
using MyAccountingApp.Domain.Interfaces;
using MyAccountingApp.Domain.ValueObjects;

namespace MyAccountingApp.Application.Services;

public class PositionEngine : IPositionEngine
{
    private readonly IPortfolioRepository _portfolioRepo;
    private readonly IMarketPriceService _marketPriceService;

    public PositionEngine(IPortfolioRepository portfolioRepo, IMarketPriceService marketPriceService)
    {
        this._portfolioRepo = portfolioRepo;
        this._marketPriceService = marketPriceService;
    }

    public async Task<PortfolioPositionDto?> GetPosition(string symbol, bool includePrice = true)
    {
        var transactions = this._portfolioRepo.GetAssetTransactions(symbol).ToList();

        if (transactions.Count == 0)
        {
            return null;
        }

        FifoPosition position = FifoCalculator.Compute(transactions);
        string currency = transactions[0].Transaction.Money.Currency;

        decimal avgCost = position.NetQuantity > 0 ? Math.Round(position.TotalCostBasis / position.NetQuantity, 4) : 0;

        Money? marketPrice = includePrice && position.NetQuantity > 0 ? await this._marketPriceService.GetPriceAsync(symbol) : null;

        decimal? unrealizedGainLoss = marketPrice is not null && position.NetQuantity > 0
            ? Math.Round((marketPrice.Amount - avgCost) * position.NetQuantity, 2)
            : null;

        return new PortfolioPositionDto(
            symbol,
            position.NetQuantity,
            avgCost,
            Math.Round(position.TotalCostBasis, 2),
            currency,
            position.TransactionCount,
            Math.Round(position.RealizedGainLoss, 2),
            position.OpenLots
                .Select(l => new TaxLotDto(
                    l.PurchaseDate,
                    l.RemainingQuantity,
                    Math.Round(l.UnitaryCost, 4),
                    Math.Round(l.RemainingQuantity * l.UnitaryCost, 2)))
                .ToList(),
            marketPrice?.Amount,
            unrealizedGainLoss,
            position.UnmatchedSellQuantity > 0,
            Math.Round(position.UnmatchedSellQuantity, 4));
    }
}
