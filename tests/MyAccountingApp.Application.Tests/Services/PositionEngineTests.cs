using MyAccountingApp.Application.Services;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Interfaces;
using MyAccountingApp.Domain.ValueObjects;
using MyAccountingApp.TestUtilities.Fakes;

namespace MyAccountingApp.Application.Tests.Services;

public class PositionEngineTests
{
    private static AssetTransaction Buy(string symbol, decimal price, decimal quantity, DateTime date)
    {
        Money money = new(price * quantity, "USD");
        Transaction tx = new(
            Guid.NewGuid(),
            date,
            $"Buy {symbol}",
            money,
            TransactionCategory.EXPENSE);
        return new AssetTransaction(tx, symbol, quantity, AssetTransactionType.Buy);
    }

    private static AssetTransaction Sell(string symbol, decimal price, decimal quantity, DateTime date)
    {
        Money money = new(price * quantity, "USD");
        Transaction tx = new(
            Guid.NewGuid(),
            date,
            $"Sell {symbol}",
            money,
            TransactionCategory.INCOME);
        return new AssetTransaction(tx, symbol, quantity, AssetTransactionType.Sell);
    }

    [Fact]
    public async Task GetPosition_ReturnsNull_WhenNoTransactions()
    {
        FakePortfolioRepo repo = new();
        FakeMarketPriceService priceService = new();
        PositionEngine engine = new(repo, priceService);

        var result = await engine.GetPosition("AAPL");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetPosition_WithSingleBuy_ReturnsCorrectPosition()
    {
        DateTime date = new(2024, 1, 15);
        FakePortfolioRepo repo = new();
        repo.AddOrUpdate(Buy("AAPL", 150, 10, date));
        FakeMarketPriceService priceService = new();
        PositionEngine engine = new(repo, priceService);

        var result = await engine.GetPosition("AAPL");

        Assert.NotNull(result);
        Assert.Equal("AAPL", result.Symbol);
        Assert.Equal(10, result.NetQuantity);
        Assert.Equal(150m, result.AverageUnitaryCost);
        Assert.Equal(1500m, result.TotalCostBasis);
        Assert.Equal("USD", result.Currency);
        Assert.Equal(1, result.TransactionCount);
        Assert.Equal(0, result.RealizedGainLoss);
        Assert.Single(result.OpenLots);
        Assert.Equal(date, result.OpenLots[0].PurchaseDate);
        Assert.Equal(10, result.OpenLots[0].Quantity);
        Assert.Equal(150m, result.OpenLots[0].UnitaryCost);
        Assert.Equal(1500m, result.OpenLots[0].TotalCost);
        Assert.Equal(150.25m, result.MarketPrice);
        Assert.Equal(2.50m, result.UnrealizedGainLoss);
    }

    [Fact]
    public async Task GetPosition_WithMultipleBuys_AveragesCorrectly()
    {
        DateTime date1 = new(2024, 1, 15);
        DateTime date2 = new(2024, 2, 1);
        FakePortfolioRepo repo = new();
        repo.AddOrUpdate(Buy("AAPL", 100, 10, date1));
        repo.AddOrUpdate(Buy("AAPL", 200, 10, date2));
        FakeMarketPriceService priceService = new();
        PositionEngine engine = new(repo, priceService);

        var result = await engine.GetPosition("AAPL");

        Assert.NotNull(result);
        Assert.Equal(20, result.NetQuantity);
        Assert.Equal(150m, result.AverageUnitaryCost);
        Assert.Equal(3000m, result.TotalCostBasis);
        Assert.Equal(2, result.OpenLots.Count);
    }

    [Fact]
    public async Task GetPosition_WithBuyThenFullSell_CalculatesRealizedPnLCorrectly()
    {
        DateTime buyDate = new(2024, 1, 15);
        DateTime sellDate = new(2024, 6, 1);
        FakePortfolioRepo repo = new();
        repo.AddOrUpdate(Buy("AAPL", 100, 10, buyDate));
        repo.AddOrUpdate(Sell("AAPL", 150, 10, sellDate));
        FakeMarketPriceService priceService = new();
        PositionEngine engine = new(repo, priceService);

        var result = await engine.GetPosition("AAPL");

        Assert.NotNull(result);
        Assert.Equal(0, result.NetQuantity);
        Assert.Equal(0, result.TotalCostBasis);
        Assert.Equal(500m, result.RealizedGainLoss);
        Assert.Empty(result.OpenLots);
        Assert.Null(result.MarketPrice);
        Assert.Null(result.UnrealizedGainLoss);
    }

    [Fact]
    public async Task GetPosition_WithBuyThenPartialSell_UsesCorrectFifoLot()
    {
        DateTime buyDate1 = new(2024, 1, 15);
        DateTime buyDate2 = new(2024, 2, 1);
        DateTime sellDate = new(2024, 6, 1);
        FakePortfolioRepo repo = new();
        repo.AddOrUpdate(Buy("AAPL", 100, 10, buyDate1));
        repo.AddOrUpdate(Buy("AAPL", 200, 10, buyDate2));
        repo.AddOrUpdate(Sell("AAPL", 150, 5, sellDate));
        FakeMarketPriceService priceService = new();
        PositionEngine engine = new(repo, priceService);

        var result = await engine.GetPosition("AAPL");

        Assert.NotNull(result);
        Assert.Equal(15, result.NetQuantity);
        Assert.Equal(5, result.OpenLots[0].Quantity);
        Assert.Equal(10, result.OpenLots[1].Quantity);
        Assert.Equal(250m, result.RealizedGainLoss);
    }

    [Fact]
    public async Task GetPosition_WithNoMarketPrice_SetsUnrealizedToNull()
    {
        FakePortfolioRepo repo = new();
        repo.AddOrUpdate(Buy("UNKN", 100, 10, new DateTime(2024, 1, 15)));
        FakeMarketPriceService priceService = new();
        PositionEngine engine = new(repo, priceService);

        var result = await engine.GetPosition("UNKN");

        Assert.NotNull(result);
        Assert.Null(result.MarketPrice);
        Assert.Null(result.UnrealizedGainLoss);
    }

    private sealed class FakePortfolioRepo : IPortfolioRepository
    {
        private readonly List<AssetTransaction> _transactions = new();

        public void AddOrUpdate(AssetTransaction assetTransaction) =>
            this._transactions.Add(assetTransaction);

        public IEnumerable<AssetTransaction> GetAssetTransactions(string symbol) =>
            this._transactions.Where(t => t.Symbol == symbol);

        public IEnumerable<AssetTransaction> GetAllTransactions() =>
            this._transactions;

        public void Initialize(IEnumerable<AssetTransaction> transactions)
        {
            this._transactions.Clear();
            this._transactions.AddRange(transactions);
        }
    }
}
