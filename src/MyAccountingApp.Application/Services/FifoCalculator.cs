using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;

namespace MyAccountingApp.Application.Services;

public sealed class FifoPosition
{
    public required IReadOnlyList<FifoLot> OpenLots { get; init; }
    public required IReadOnlyList<FifoSale> Sales { get; init; }
    public decimal RealizedGainLoss { get; init; }
    public decimal NetQuantity { get; init; }
    public decimal TotalCostBasis { get; init; }
    public decimal UnmatchedSellQuantity { get; init; }
    public int TransactionCount { get; init; }
}

public sealed class FifoLot
{
    public DateTime PurchaseDate { get; }
    public decimal Quantity { get; }
    public decimal TotalCost { get; }
    public decimal UnitaryCost => this.TotalCost / this.Quantity;
    public decimal RemainingQuantity { get; internal set; }

    internal FifoLot(DateTime purchaseDate, decimal quantity, decimal totalCost)
    {
        this.PurchaseDate = purchaseDate;
        this.Quantity = quantity;
        this.TotalCost = totalCost;
        this.RemainingQuantity = quantity;
    }
}

public sealed class FifoSale
{
    public DateTime Date { get; }
    public decimal Quantity { get; }
    public decimal Proceeds { get; }
    public decimal CostBasis { get; }
    public decimal RealizedGainLoss { get; }

    internal FifoSale(DateTime date, decimal quantity, decimal proceeds, decimal costBasis)
    {
        this.Date = date;
        this.Quantity = quantity;
        this.Proceeds = proceeds;
        this.CostBasis = costBasis;
        this.RealizedGainLoss = proceeds - costBasis;
    }
}

public static class FifoCalculator
{
    public static FifoPosition Compute(IEnumerable<AssetTransaction> transactions)
    {
        List<AssetTransaction> ordered = transactions
            .OrderBy(t => t.Transaction.Date)
            .ToList();

        List<FifoLot> lots = new();
        List<FifoSale> sales = new();
        decimal realizedGainLoss = 0;
        decimal totalCost = 0;
        decimal netQuantity = 0;
        decimal unmatchedSellQuantity = 0;

        foreach (AssetTransaction tx in ordered)
        {
            if (tx.Type == AssetTransactionType.Buy)
            {
                FifoLot lot = new(tx.Transaction.Date, tx.Quantity, tx.Transaction.Money.Amount);
                lots.Add(lot);
                netQuantity += tx.Quantity;
                totalCost += tx.Transaction.Money.Amount;
            }
            else
            {
                decimal sellQty = tx.Quantity;
                decimal matchedQty = 0;
                decimal matchedProceeds = 0;
                decimal matchedCostBasis = 0;

                foreach (FifoLot lot in lots.Where(l => l.RemainingQuantity > 0).OrderBy(l => l.PurchaseDate))
                {
                    if (sellQty <= 0)
                    {
                        break;
                    }

                    decimal consumed = Math.Min(sellQty, lot.RemainingQuantity);
                    decimal costBasis = consumed * lot.UnitaryCost;
                    decimal proceeds = (consumed / tx.Quantity) * tx.Transaction.Money.Amount;

                    matchedQty += consumed;
                    matchedProceeds += proceeds;
                    matchedCostBasis += costBasis;
                    realizedGainLoss += proceeds - costBasis;
                    totalCost -= costBasis;
                    netQuantity -= consumed;

                    lot.RemainingQuantity -= consumed;
                    sellQty -= consumed;
                }

                if (matchedQty > 0)
                {
                    sales.Add(new FifoSale(tx.Transaction.Date, matchedQty, matchedProceeds, matchedCostBasis));
                }

                if (sellQty > 0)
                {
                    unmatchedSellQuantity += sellQty;
                }
            }
        }

        return new FifoPosition
        {
            OpenLots = lots.Where(l => l.RemainingQuantity > 0).ToList(),
            Sales = sales,
            RealizedGainLoss = realizedGainLoss,
            NetQuantity = netQuantity,
            TotalCostBasis = totalCost,
            UnmatchedSellQuantity = unmatchedSellQuantity,
            TransactionCount = ordered.Count,
        };
    }
}
