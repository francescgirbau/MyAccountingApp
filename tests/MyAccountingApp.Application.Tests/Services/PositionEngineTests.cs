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

    private static PositionEngine CreateEngine(FakePortfolioRepo repo, IMarketPriceService priceService) =>
        new(repo, new FakeOptionRepository(), priceService);

    [Fact]
    public async Task GetPosition_ReturnsNull_WhenNoTransactions()
    {
        FakePortfolioRepo repo = new();
        FakeMarketPriceService priceService = new();
        PositionEngine engine = CreateEngine(repo, priceService);

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
        PositionEngine engine = CreateEngine(repo, priceService);

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
        PositionEngine engine = CreateEngine(repo, priceService);

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
        PositionEngine engine = CreateEngine(repo, priceService);

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
        PositionEngine engine = CreateEngine(repo, priceService);

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
        PositionEngine engine = CreateEngine(repo, priceService);

        var result = await engine.GetPosition("UNKN");

        Assert.NotNull(result);
        Assert.Null(result.MarketPrice);
        Assert.Null(result.UnrealizedGainLoss);
    }

    [Fact]
    public async Task GetPosition_WithSellExceedingPosition_FlagsShortfall()
    {
        DateTime buyDate = new(2024, 1, 15);
        DateTime sellDate = new(2024, 6, 1);
        FakePortfolioRepo repo = new();
        repo.AddOrUpdate(Buy("AAPL", 100, 10, buyDate));
        repo.AddOrUpdate(Sell("AAPL", 150, 15, sellDate));
        FakeMarketPriceService priceService = new();
        PositionEngine engine = CreateEngine(repo, priceService);

        var result = await engine.GetPosition("AAPL");

        Assert.NotNull(result);
        Assert.True(result.HasShortfall);
        Assert.Equal(5, result.UnmatchedSellQuantity);
        Assert.Equal(0, result.NetQuantity);
        Assert.Equal(0, result.TotalCostBasis);
        Assert.Equal(500m, result.RealizedGainLoss);
        Assert.Empty(result.OpenLots);
        Assert.Null(result.MarketPrice);
        Assert.Null(result.UnrealizedGainLoss);
    }

    [Fact]
    public async Task GetPosition_WithFullSell_DoesNotFlagShortfall()
    {
        DateTime buyDate = new(2024, 1, 15);
        DateTime sellDate = new(2024, 6, 1);
        FakePortfolioRepo repo = new();
        repo.AddOrUpdate(Buy("AAPL", 100, 10, buyDate));
        repo.AddOrUpdate(Sell("AAPL", 150, 10, sellDate));
        FakeMarketPriceService priceService = new();
        PositionEngine engine = CreateEngine(repo, priceService);

        var result = await engine.GetPosition("AAPL");

        Assert.NotNull(result);
        Assert.False(result.HasShortfall);
        Assert.Equal(0, result.UnmatchedSellQuantity);
        Assert.Equal(0, result.NetQuantity);
    }

    [Fact]
    public async Task GetPosition_WithMultipleShortfallSells_AccumulatesUnmatchedQuantity()
    {
        DateTime buyDate = new(2024, 1, 15);
        FakePortfolioRepo repo = new();
        repo.AddOrUpdate(Buy("AAPL", 100, 10, buyDate));
        repo.AddOrUpdate(Sell("AAPL", 150, 7, new DateTime(2024, 6, 1)));
        repo.AddOrUpdate(Sell("AAPL", 150, 5, new DateTime(2024, 7, 1)));
        FakeMarketPriceService priceService = new();
        PositionEngine engine = CreateEngine(repo, priceService);

        var result = await engine.GetPosition("AAPL");

        Assert.NotNull(result);
        Assert.True(result.HasShortfall);
        Assert.Equal(2, result.UnmatchedSellQuantity);
        Assert.Equal(0, result.NetQuantity);
    }

    [Fact]
    public async Task GetPosition_WithoutPrice_SkipsPriceService()
    {
        FakePortfolioRepo repo = new();
        repo.AddOrUpdate(Buy("AAPL", 150, 10, new DateTime(2024, 1, 15)));
        ThrowingMarketPriceService priceService = new();
        PositionEngine engine = CreateEngine(repo, priceService);

        var result = await engine.GetPosition("AAPL", includePrice: false);

        Assert.NotNull(result);
        Assert.Null(result.MarketPrice);
        Assert.Null(result.UnrealizedGainLoss);
        Assert.Equal(10, result.NetQuantity);
    }

    [Fact]
    public async Task GetPosition_WithLongOption_ReturnsOptionPosition()
    {
        FakePortfolioRepo repo = new();
        FakeOptionRepository optionRepo = new();
        optionRepo.Add(Opt("VET", 30, 2, AssetTransactionType.Buy, "EUR"));
        PositionEngine engine = new(repo, optionRepo, new FakeMarketPriceService(new Dictionary<string, Money> { { "VET", new Money(35m, "EUR") } }));

        var result = await engine.GetPosition("VET");

        Assert.NotNull(result);
        Assert.Equal("Option", result.AssetClass);
        Assert.Equal(2, result.NetQuantity);
        Assert.Equal(60m, result.TotalCostBasis);
        Assert.Equal(30m, result.AverageUnitaryCost);
        Assert.Equal(35m, result.MarketPrice);
        Assert.Equal(10m, result.UnrealizedGainLoss);
        Assert.False(result.HasShortfall);
    }

    [Fact]
    public async Task GetPosition_WithShortOption_ReturnsNegativeCostAndUnrealized()
    {
        FakePortfolioRepo repo = new();
        FakeOptionRepository optionRepo = new();
        // Open short: sell 3 @100 premium, current price 80 -> gain 60, market value -240.
        optionRepo.Add(Opt("SPX", 100, 3, AssetTransactionType.Sell, "EUR"));
        PositionEngine engine = new(repo, optionRepo, new FakeMarketPriceService(new Dictionary<string, Money> { { "SPX", new Money(80m, "EUR") } }));

        var result = await engine.GetPosition("SPX");

        Assert.NotNull(result);
        Assert.Equal("Option", result.AssetClass);
        Assert.Equal(-3, result.NetQuantity);
        Assert.Equal(-300m, result.TotalCostBasis);
        Assert.Equal(100m, result.AverageUnitaryCost);
        Assert.Equal(80m, result.MarketPrice);
        Assert.Equal(60m, result.UnrealizedGainLoss);
        Assert.False(result.HasShortfall);
        Assert.Equal(0, result.UnmatchedSellQuantity);
    }

    [Fact]
    public async Task GetPosition_WithShortOptionClosed_ComputesRealizedGain()
    {
        FakePortfolioRepo repo = new();
        FakeOptionRepository optionRepo = new();
        optionRepo.Add(Opt("SPX", 100, 3, AssetTransactionType.Sell, "EUR", new DateTime(2024, 1, 10)));
        optionRepo.Add(Opt("SPX", 80, 3, AssetTransactionType.Buy, "EUR", new DateTime(2024, 3, 10)));
        PositionEngine engine = new(repo, optionRepo, new FakeMarketPriceService(new Dictionary<string, Money> { { "SPX", new Money(80m, "EUR") } }));

        var result = await engine.GetPosition("SPX");

        Assert.NotNull(result);
        Assert.Equal(0, result.NetQuantity);
        Assert.Equal(60m, result.RealizedGainLoss);
        Assert.False(result.HasShortfall);
    }

    [Fact]
    public async Task GetPosition_MergesStockAndOptionForSameSymbol_AsMixed()
    {
        FakePortfolioRepo repo = new();
        repo.AddOrUpdate(Buy("VET", 50, 10, new DateTime(2024, 1, 10)));
        FakeOptionRepository optionRepo = new();
        optionRepo.Add(Opt("VET", 100, 3, AssetTransactionType.Sell, "EUR", new DateTime(2024, 2, 10)));
        PositionEngine engine = new(repo, optionRepo, new FakeMarketPriceService(new Dictionary<string, Money> { { "VET", new Money(60m, "EUR") } }));

        var result = await engine.GetPosition("VET");

        Assert.NotNull(result);
        Assert.Equal("Mixed", result.AssetClass);
        Assert.Equal(7, result.NetQuantity); // 10 stock - 3 short options
        Assert.Equal(200m, result.TotalCostBasis); // 500 - 300 credit
        Assert.Equal(2, result.OpenLots.Count);
    }

    private static OptionTransaction Opt(string symbol, decimal premium, decimal quantity, AssetTransactionType type, string currency, DateTime? date = null)
    {
        DateTime d = date ?? new DateTime(2024, 1, 15);
        TransactionCategory category = type == AssetTransactionType.Buy
            ? TransactionCategory.INVESTMENT
            : TransactionCategory.DIVESTMENT;
        Transaction tx = new(Guid.NewGuid(), d, $"Opt {symbol}", new Money(premium * quantity, currency), category);
        return new OptionTransaction(tx, symbol, "ISIN", quantity, type);
    }

    private sealed class ThrowingMarketPriceService : IMarketPriceService
    {
        public Task<Money?> GetPriceAsync(string symbol) => throw new InvalidOperationException("Price service should not be called");

        public Task<Money?> RefreshPriceAsync(string symbol) => throw new InvalidOperationException("Price service should not be called");

        public Task<Money?> GetCachedPriceAsync(string symbol) => throw new InvalidOperationException("Price service should not be called");

        public Task<CachedQuote?> GetLastQuoteAsync(string symbol) => throw new InvalidOperationException("Price service should not be called");
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

        public bool Delete(Guid transactionId) => true;
        public int DeleteByYear(int year) => this._transactions.RemoveAll(t => t.Transaction.Date.Year == year);
    }
}
