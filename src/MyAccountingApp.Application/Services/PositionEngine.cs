using MyAccountingApp.Application.DTOs;
using MyAccountingApp.Application.Interfaces;
using MyAccountingApp.Domain.Interfaces;
using MyAccountingApp.Domain.ValueObjects;

namespace MyAccountingApp.Application.Services;

public class PositionEngine : IPositionEngine
{
    private readonly IPortfolioRepository _portfolioRepo;
    private readonly IOptionTransactionRepository _optionRepo;
    private readonly IMarketPriceService _marketPriceService;

    public PositionEngine(
        IPortfolioRepository portfolioRepo,
        IOptionTransactionRepository optionRepo,
        IMarketPriceService marketPriceService)
    {
        this._portfolioRepo = portfolioRepo;
        this._optionRepo = optionRepo;
        this._marketPriceService = marketPriceService;
    }

    public async Task<PortfolioPositionDto?> GetPosition(string symbol, bool includePrice = true)
    {
        var assetTransactions = this._portfolioRepo.GetAssetTransactions(symbol).ToList();
        var optionTransactions = this._optionRepo.GetAll().Where(o => o.Symbol == symbol).ToList();

        if (assetTransactions.Count == 0 && optionTransactions.Count == 0)
        {
            return null;
        }

        FifoPosition? stockPosition = assetTransactions.Count > 0 ? FifoCalculator.Compute(assetTransactions) : null;
        FifoPosition? optionPosition = optionTransactions.Count > 0 ? FifoCalculator.ComputeOptions(optionTransactions) : null;
        FifoPosition position = stockPosition is not null && optionPosition is not null
            ? FifoCalculator.Merge(stockPosition, optionPosition)
            : stockPosition ?? optionPosition!;

        string currency = assetTransactions.Count > 0
            ? assetTransactions[0].Transaction.Money.Currency
            : optionTransactions[0].Transaction.Money.Currency;
        bool hasOptions = optionTransactions.Count > 0;

        decimal avgCost = position.NetQuantity != 0 ? Math.Round(position.TotalCostBasis / position.NetQuantity, 4) : 0;

        bool priceEnabled = includePrice && position.NetQuantity != 0;
        Money? marketPrice = priceEnabled ? await this._marketPriceService.GetPriceAsync(symbol) : null;

        decimal? unrealizedGainLoss = marketPrice is not null && position.NetQuantity != 0
            ? Math.Round((marketPrice.Amount - avgCost) * position.NetQuantity, 2)
            : null;

        string assetClass = hasOptions
            ? (assetTransactions.Count > 0 ? "Mixed" : "Option")
            : "Stock";

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
            Math.Round(position.UnmatchedSellQuantity, 4),
            assetClass);
    }
}