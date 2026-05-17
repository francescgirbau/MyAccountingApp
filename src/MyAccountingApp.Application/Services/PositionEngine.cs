using MyAccountingApp.Application.DTOs;
using MyAccountingApp.Application.Interfaces;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Interfaces;

namespace MyAccountingApp.Application.Services;

public class PositionEngine : IPositionEngine
{
    private readonly IPortfolioRepository _portfolioRepo;

    public PositionEngine(IPortfolioRepository portfolioRepo)
    {
        this._portfolioRepo = portfolioRepo;
    }

    public PortfolioPositionDto? GetPosition(string symbol)
    {
        var transactions = this._portfolioRepo.GetAssetTransactions(symbol)
            .OrderBy(t => t.Transaction.Date)
            .ToList();

        if (transactions.Count == 0)
        {
            return null;
        }

        var lots = new List<FifoLot>();
        decimal realizedGainLoss = 0;
        string currency = transactions[0].Transaction.Money.Currency;
        decimal totalCost = 0;
        decimal netQuantity = 0;

        foreach (var tx in transactions)
        {
            if (tx.Type == AssetTransactionType.Buy)
            {
                var lot = new FifoLot(tx.Transaction.Date, tx.Quantity, tx.Transaction.Money.Amount);
                lots.Add(lot);
                netQuantity += tx.Quantity;
                totalCost += tx.Transaction.Money.Amount;
            }
            else
            {
                decimal sellQty = tx.Quantity;
                netQuantity -= sellQty;

                foreach (var lot in lots.Where(l => l.RemainingQuantity > 0).OrderBy(l => l.PurchaseDate))
                {
                    if (sellQty <= 0)
                    {
                        break;
                    }

                    decimal consumed = Math.Min(sellQty, lot.RemainingQuantity);
                    decimal costBasis = consumed * lot.UnitaryCost;
                    decimal proceeds = (consumed / tx.Quantity) * tx.Transaction.Money.Amount;

                    realizedGainLoss += proceeds - costBasis;
                    totalCost -= costBasis;

                    lot.RemainingQuantity -= consumed;
                    sellQty -= consumed;
                }
            }
        }

        decimal avgCost = netQuantity > 0 ? Math.Round(totalCost / netQuantity, 4) : 0;

        return new PortfolioPositionDto(
            symbol,
            netQuantity,
            avgCost,
            Math.Round(totalCost, 2),
            currency,
            transactions.Count,
            Math.Round(realizedGainLoss, 2),
            lots.Where(l => l.RemainingQuantity > 0)
                .Select(l => new TaxLotDto(
                    l.PurchaseDate,
                    l.RemainingQuantity,
                    Math.Round(l.UnitaryCost, 4),
                    Math.Round(l.RemainingQuantity * l.UnitaryCost, 2)))
                .ToList());
    }

    private sealed class FifoLot
    {
        public DateTime PurchaseDate { get; }
        public decimal TotalQuantity { get; }
        public decimal TotalCost { get; }
        public decimal UnitaryCost => TotalCost / TotalQuantity;
        public decimal RemainingQuantity { get; set; }

        public FifoLot(DateTime purchaseDate, decimal quantity, decimal totalCost)
        {
            this.PurchaseDate = purchaseDate;
            this.TotalQuantity = quantity;
            this.TotalCost = totalCost;
            this.RemainingQuantity = quantity;
        }
    }
}
