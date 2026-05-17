using MyAccountingApp.Application.DTOs;
using MyAccountingApp.Application.Interfaces;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Interfaces;

namespace MyAccountingApp.Application.Services;

public class PortfolioQuery : IPortfolioQuery
{
    private readonly IPortfolioRepository _portfolioRepo;

    public PortfolioQuery(IPortfolioRepository portfolioRepo)
    {
        this._portfolioRepo = portfolioRepo;
    }

    public PortfolioPositionDto? GetPosition(string symbol)
    {
        var transactions = this._portfolioRepo.GetAssetTransactions(symbol).ToList();

        if (transactions.Count == 0)
        {
            return null;
        }

        decimal netQuantity = 0;
        decimal totalCost = 0;
        string currency = transactions[0].Transaction.Money.Currency;

        foreach (var tx in transactions)
        {
            if (tx.Type == AssetTransactionType.Buy)
            {
                netQuantity += tx.Quantity;
                totalCost += tx.Transaction.Money.Amount;
            }
            else
            {
                netQuantity -= tx.Quantity;
                totalCost -= tx.Transaction.Money.Amount;
            }
        }

        decimal avgCost = netQuantity > 0 ? Math.Round(totalCost / netQuantity, 4) : 0;

        return new PortfolioPositionDto(
            symbol,
            netQuantity,
            avgCost,
            Math.Round(totalCost, 2),
            currency,
            transactions.Count);
    }
}
