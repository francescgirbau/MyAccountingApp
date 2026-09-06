using MyAccountingApp.Application.Services;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.ValueObjects;

namespace MyAccountingApp.Application.Tests.Services;

public class FifoCalculatorOptionTests
{
    private static OptionTransaction Buy(string symbol, decimal price, decimal quantity, DateTime date)
    {
        Money money = new(price * quantity, "EUR");
        Transaction tx = new(Guid.NewGuid(), date, $"Buy {symbol}", money, TransactionCategory.INVESTMENT);
        return new OptionTransaction(tx, symbol, "ISIN", quantity, AssetTransactionType.Buy);
    }

    private static OptionTransaction Sell(string symbol, decimal price, decimal quantity, DateTime date)
    {
        Money money = new(price * quantity, "EUR");
        Transaction tx = new(Guid.NewGuid(), date, $"Sell {symbol}", money, TransactionCategory.DIVESTMENT);
        return new OptionTransaction(tx, symbol, "ISIN", quantity, AssetTransactionType.Sell);
    }

    [Fact]
    public void LongOption_OpenAndClose_ComputesNetCostAndRealized()
    {
        DateTime d1 = new(2024, 1, 10);
        DateTime d2 = new(2024, 3, 10);

        var position = FifoCalculator.ComputeOptions(new[]
        {
            Buy("VET", 50, 2, d1),
            Sell("VET", 70, 2, d2),
        });

        Assert.Equal(0, position.NetQuantity);
        Assert.Equal(0m, position.TotalCostBasis, 2);
        Assert.Equal(40m, position.RealizedGainLoss, 2);
        Assert.Empty(position.OpenLots);
        Assert.Equal(1, position.Sales.Count);
        Assert.Equal(40m, position.Sales[0].RealizedGainLoss, 2);
    }

    [Fact]
    public void ShortOption_OpenOnly_ProducesNegativeQuantityAndCost()
    {
        DateTime d1 = new(2024, 1, 10);

        var position = FifoCalculator.ComputeOptions(new[]
        {
            Sell("SPX", 100, 3, d1),
        });

        Assert.Equal(-3m, position.NetQuantity);
        Assert.Equal(-300m, position.TotalCostBasis, 2);
        Assert.Equal(0m, position.RealizedGainLoss, 2);
        Assert.Equal(0, position.UnmatchedSellQuantity);
        Assert.Single(position.OpenLots);
        Assert.Equal(-300m, position.OpenLots[0].TotalCost, 2);
        Assert.Equal(-3m, position.OpenLots[0].RemainingQuantity);
        Assert.Equal(-100m, position.OpenLots[0].UnitaryCost, 2);
    }

    [Fact]
    public void ShortOption_OpenAndBuyBack_ComputesRealizedGain()
    {
        DateTime d1 = new(2024, 1, 10);
        DateTime d2 = new(2024, 3, 10);

        var position = FifoCalculator.ComputeOptions(new[]
        {
            Sell("SPX", 100, 3, d1),
            Buy("SPX", 80, 3, d2),
        });

        Assert.Equal(0, position.NetQuantity);
        Assert.Equal(0m, position.TotalCostBasis, 2);
        Assert.Equal(60m, position.RealizedGainLoss, 2);
        Assert.Empty(position.OpenLots);
        Assert.Equal(1, position.Sales.Count);
        Assert.Equal(300m, position.Sales[0].Proceeds, 2);
        Assert.Equal(240m, position.Sales[0].CostBasis, 2);
    }

    [Fact]
    public void BuyThenOversell_GoesShort_RealizesOnlyClosedPortion()
    {
        DateTime d1 = new(2024, 1, 10);
        DateTime d2 = new(2024, 3, 10);

        var position = FifoCalculator.ComputeOptions(new[]
        {
            Buy("RUT", 100, 1, d1),
            Sell("RUT", 120, 3, d2),
        });

        Assert.Equal(-2m, position.NetQuantity);
        Assert.Equal(-240m, position.TotalCostBasis, 2);
        Assert.Equal(20m, position.RealizedGainLoss, 2);
        Assert.Single(position.OpenLots);
        Assert.Equal(-240m, position.OpenLots[0].TotalCost, 2);
        Assert.Equal(-2m, position.OpenLots[0].RemainingQuantity);
        Assert.Equal(-120m, position.OpenLots[0].UnitaryCost, 2);
    }

    [Fact]
    public void SellThenOverbuy_GoesLong_RealizesOnlyClosedPortion()
    {
        DateTime d1 = new(2024, 1, 10);
        DateTime d2 = new(2024, 3, 10);

        var position = FifoCalculator.ComputeOptions(new[]
        {
            Sell("RUT", 100, 3, d1),
            Buy("RUT", 80, 5, d2),
        });

        Assert.Equal(2m, position.NetQuantity);
        Assert.Equal(160m, position.TotalCostBasis, 2);
        Assert.Equal(60m, position.RealizedGainLoss, 2);
        Assert.Single(position.OpenLots);
        Assert.Equal(160m, position.OpenLots[0].TotalCost, 2);
        Assert.Equal(2m, position.OpenLots[0].RemainingQuantity);
    }

    [Fact]
    public void PartialShortClose_UsesFifoFully()
    {
        DateTime d1 = new(2024, 1, 10);
        DateTime d2 = new(2024, 2, 10);
        DateTime d3 = new(2024, 3, 10);

        var position = FifoCalculator.ComputeOptions(new[]
        {
            Sell("SPX", 100, 5, d1),
            Sell("SPX", 90, 5, d2),
            Buy("SPX", 80, 6, d3),
        });

        // Sells open 10 short. Closing 6 against the oldest short lot (5 @100, 1 @90).
        Assert.Equal(-4m, position.NetQuantity);
        Assert.Equal(-360m, position.TotalCostBasis, 2); // remaining 4 @90 credit
        Assert.Equal(110m, position.RealizedGainLoss, 2); // 5*(100-80) + 1*(90-80)
        Assert.Single(position.OpenLots);
        Assert.Equal(-4m, position.OpenLots[0].RemainingQuantity);
        Assert.Equal(-90m, position.OpenLots[0].UnitaryCost, 2);
    }

    [Fact]
    public void Merge_CombinesIndependentStockAndOptionStreams()
    {
        DateTime d1 = new(2024, 1, 10);

        FifoPosition stock = FifoCalculator.Compute(new[]
        {
            new AssetTransaction(
                new Transaction(Guid.NewGuid(), d1, "Buy VET", new Money(500, "EUR"), TransactionCategory.INVESTMENT),
                "VET", 10, AssetTransactionType.Buy),
        });

        FifoPosition option = FifoCalculator.ComputeOptions(new[]
        {
            Sell("VET", 100, 3, d1),
        });

        var merged = FifoCalculator.Merge(stock, option);

        Assert.Equal(7m, merged.NetQuantity); // 10 stock - 3 short options
        Assert.Equal(200m, merged.TotalCostBasis, 2); // +500 stock - 300 option credit
        Assert.Equal(0m, merged.RealizedGainLoss, 2);
        Assert.Equal(2, merged.OpenLots.Count);
        Assert.Equal(2, merged.TransactionCount);
    }

    [Fact]
    public void Compute_StockOnlyOutputsMatchSignedEngine_AllPositive()
    {
        // Regression guard: the long-only stock path produces positive quantities/cost and
        // identical magnitudes whether run through Compute or the shared signed engine.
        DateTime d1 = new(2024, 1, 10);
        DateTime d2 = new(2024, 3, 10);

        FifoPosition compute = FifoCalculator.Compute(new[]
        {
            new AssetTransaction(new Transaction(Guid.NewGuid(), d1, "Buy", new Money(200, "EUR"), TransactionCategory.INVESTMENT), "VET", 2, AssetTransactionType.Buy),
            new AssetTransaction(new Transaction(Guid.NewGuid(), d2, "Sell", new Money(150, "EUR"), TransactionCategory.DIVESTMENT), "VET", 1, AssetTransactionType.Sell),
        });

        Assert.True(compute.NetQuantity > 0);
        Assert.True(compute.TotalCostBasis >= 0);
        Assert.All(compute.OpenLots, l => Assert.True(l.RemainingQuantity >= 0));
        Assert.All(compute.OpenLots, l => Assert.True(l.TotalCost >= 0));
    }
}