using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;

namespace MyAccountingApp.Application.Services;

public sealed class FifoPosition
{
    required public IReadOnlyList<FifoLot> OpenLots { get; init; }
    required public IReadOnlyList<FifoSale> Sales { get; init; }
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
        List<FifoTrade> trades = transactions
            .OrderBy(t => t.Transaction.Date)
            .Select(t => new FifoTrade(
                t.Transaction.Date,
                t.Quantity,
                t.Type == AssetTransactionType.Buy,
                t.Transaction.Money.Amount))
            .ToList();

        return ComputeCore(trades, allowShorts: false);
    }

    /// <summary>
    /// Computes a signed FIFO position for option transactions. Options are treated like
    /// instruments that can be both long (opened by a Buy) and short (opened by a Sell that
    /// exceeds the current long quantity). Realized P/L is direction-agnostic
    /// (<c>proceeds - costBasis</c> per matched quantity).
    /// </summary>
    /// <remarks>
    /// Sign conventions: NetQuantity is positive for a net-long position and negative for a
    /// net-short position. TotalCostBasis is the signed net cost: positive while holding
    /// long (premiums paid), negative while holding short (premiums received are credits).
    /// A FifoLot with a negative RemainingQuantity represents an open short lot; its
    /// TotalCost/UnitaryCost are negative (credit per unit).
    ///
    /// Option symbols produced by the importers may collide with plain stock tickers when they
    /// share the same underlying (e.g. Degiro/IBKR strip the OCC contract to the underlying
    /// root symbol). Merging quantities across stock and option FIFO streams is therefore only
    /// meaningful when a symbol genuinely refers to a single instrument; callers must decide
    /// whether to merge or segregate. See FifoCalculator.Merge.
    /// </remarks>
    public static FifoPosition ComputeOptions(IEnumerable<OptionTransaction> transactions)
    {
        List<FifoTrade> trades = transactions
            .OrderBy(t => t.Transaction.Date)
            .Select(t => new FifoTrade(
                t.Transaction.Date,
                t.Quantity,
                t.Type == AssetTransactionType.Buy,
                t.Transaction.Money.Amount))
            .ToList();

        return ComputeCore(trades, allowShorts: true);
    }

    /// <summary>
    /// Combines a stock FIFO position and an option FIFO position for the same symbol into a
    /// single merged position by summing net quantities, cost basis, realized P/L, unmatched
    /// sells and transaction counts, and concatenating the open lots and realized sales.
    /// </summary>
    /// <remarks>
    /// Important: this only produces a meaningful result when the stock and option streams
    /// refer to the same underlying instrument and their quantities are comparable. If a stock
    /// ticker and an option root collide for unrelated instruments, the merged NetQuantity and
    /// TotalCostBasis will have no economic meaning. Callers should verify instrument identity
    /// (e.g. same underlying) before merging.
    /// </remarks>
    public static FifoPosition Merge(FifoPosition stock, FifoPosition option)
    {
        List<FifoLot> lots = new(stock.OpenLots.Count + option.OpenLots.Count);
        lots.AddRange(stock.OpenLots);
        lots.AddRange(option.OpenLots);

        List<FifoSale> sales = new(stock.Sales.Count + option.Sales.Count);
        sales.AddRange(stock.Sales);
        sales.AddRange(option.Sales);

        return new FifoPosition
        {
            OpenLots = lots,
            Sales = sales,
            RealizedGainLoss = stock.RealizedGainLoss + option.RealizedGainLoss,
            NetQuantity = stock.NetQuantity + option.NetQuantity,
            TotalCostBasis = stock.TotalCostBasis + option.TotalCostBasis,
            UnmatchedSellQuantity = stock.UnmatchedSellQuantity + option.UnmatchedSellQuantity,
            TransactionCount = stock.TransactionCount + option.TransactionCount,
        };
    }

    private static FifoPosition ComputeCore(IReadOnlyList<FifoTrade> ordered, bool allowShorts)
    {
        List<EngineLot> lots = new();
        List<FifoSale> sales = new();
        decimal realizedGainLoss = 0;
        decimal totalCost = 0;
        decimal netQuantity = 0;
        decimal unmatchedSellQuantity = 0;

        foreach (FifoTrade tx in ordered)
        {
            decimal remainingTrade = tx.Quantity;
            decimal matchedQty = 0;
            decimal matchedProceeds = 0;
            decimal matchedCostBasis = 0;

            if (tx.IsBuy)
            {
                foreach (EngineLot lot in lots.Where(l => l.RemainingQuantity < 0).OrderBy(l => l.OpenDate))
                {
                    if (remainingTrade <= 0)
                    {
                        break;
                    }

                    decimal openQty = -lot.RemainingQuantity;
                    decimal consumed = Math.Min(remainingTrade, openQty);
                    decimal proceeds = consumed * lot.UnitaryCost;
                    decimal costBasis = (consumed / tx.Quantity) * tx.Amount;

                    matchedQty += consumed;
                    matchedProceeds += proceeds;
                    matchedCostBasis += costBasis;
                    realizedGainLoss += proceeds - costBasis;
                    totalCost += proceeds;
                    netQuantity += consumed;

                    lot.RemainingQuantity += consumed;
                    remainingTrade -= consumed;
                }

                if (matchedQty > 0)
                {
                    sales.Add(new FifoSale(tx.Date, matchedQty, matchedProceeds, matchedCostBasis));
                }

                if (remainingTrade > 0)
                {
                    decimal amount = Math.Round(tx.Amount - matchedCostBasis, 2);
                    lots.Add(new EngineLot(tx.Date, amount, remainingTrade));
                    netQuantity += remainingTrade;
                    totalCost += amount;
                }
            }
            else
            {
                foreach (EngineLot lot in lots.Where(l => l.RemainingQuantity > 0).OrderBy(l => l.OpenDate))
                {
                    if (remainingTrade <= 0)
                    {
                        break;
                    }

                    decimal consumed = Math.Min(remainingTrade, lot.RemainingQuantity);
                    decimal costBasis = consumed * lot.UnitaryCost;
                    decimal proceeds = (consumed / tx.Quantity) * tx.Amount;

                    matchedQty += consumed;
                    matchedProceeds += proceeds;
                    matchedCostBasis += costBasis;
                    realizedGainLoss += proceeds - costBasis;
                    totalCost -= costBasis;
                    netQuantity -= consumed;

                    lot.RemainingQuantity -= consumed;
                    remainingTrade -= consumed;
                }

                if (matchedQty > 0)
                {
                    sales.Add(new FifoSale(tx.Date, matchedQty, matchedProceeds, matchedCostBasis));
                }

                if (remainingTrade > 0)
                {
                    if (allowShorts)
                    {
                        decimal amount = Math.Round(tx.Amount - matchedProceeds, 2);
                        lots.Add(new EngineLot(tx.Date, amount, -remainingTrade));
                        netQuantity -= remainingTrade;
                        totalCost -= amount;
                    }
                    else
                    {
                        unmatchedSellQuantity += remainingTrade;
                    }
                }
            }
        }

        List<FifoLot> openLots = lots
            .Where(l => l.RemainingQuantity != 0)
            .Select(l => new FifoLot(l.OpenDate, l.OpenQuantity, l.RemainingQuantity > 0 ? l.OpenAmount : -l.OpenAmount)
            {
                RemainingQuantity = l.RemainingQuantity,
            })
            .ToList();

        return new FifoPosition
        {
            OpenLots = openLots,
            Sales = sales,
            RealizedGainLoss = realizedGainLoss,
            NetQuantity = netQuantity,
            TotalCostBasis = totalCost,
            UnmatchedSellQuantity = unmatchedSellQuantity,
            TransactionCount = ordered.Count,
        };
    }

    internal sealed record FifoTrade(DateTime Date, decimal Quantity, bool IsBuy, decimal Amount);

    private sealed class EngineLot
    {
        public EngineLot(DateTime openDate, decimal openAmount, decimal remainingQuantity)
        {
            this.OpenDate = openDate;
            this.OpenAmount = openAmount;
            this.RemainingQuantity = remainingQuantity;
            this.OpenQuantity = remainingQuantity < 0 ? -remainingQuantity : remainingQuantity;
        }

        public DateTime OpenDate { get; }

        public decimal OpenQuantity { get; }

        public decimal OpenAmount { get; }

        public decimal RemainingQuantity { get; set; }

        public decimal UnitaryCost => this.OpenAmount / this.OpenQuantity;
    }
}
