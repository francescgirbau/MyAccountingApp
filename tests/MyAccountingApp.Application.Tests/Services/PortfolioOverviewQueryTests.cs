using MyAccountingApp.Application.DTOs;
using MyAccountingApp.Application.Services;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Interfaces;
using MyAccountingApp.Domain.ValueObjects;
using MyAccountingApp.TestUtilities.Fakes;

namespace MyAccountingApp.Application.Tests.Services;

public class PortfolioOverviewQueryTests
{
    private static readonly DateOnly AsOf = new(2025, 6, 1);

    [Fact]
    public async Task GetOverviewAsync_ComputesWeightsAndKpis_WhenTwoPricedPositions()
    {
        // Arrange: A invested 70 (current value 10), B invested 30 (current value 10) -> 70/30 at purchase, 50/50 now.
        FakePfRepo pfRepo = new(new[] { Buy("A", 10, 70), Buy("B", 10, 30) });
        FakeMarketPriceService prices = new(new Dictionary<string, Money>
        {
            { "A", new Money(1m, "EUR") },
            { "B", new Money(1m, "EUR") },
        });
        PortfolioOverviewQuery query = new(pfRepo, new FakeOptionRepo(), prices, new FakeConversionRepository());

        // Act
        PortfolioOverviewDto result = await query.GetOverviewAsync(AsOf);

        // Assert
        Assert.Equal(100m, result.InvestedCostEur);
        Assert.Equal(20m, result.MarketValueEur);
        Assert.Equal(-80m, result.UnrealizedPnLEur);
        Assert.Equal(-0.8m, result.UnrealizedPnLPct);

        PortfolioPositionRowDto a = result.Positions.Single(p => p.Symbol == "A");
        PortfolioPositionRowDto b = result.Positions.Single(p => p.Symbol == "B");
        Assert.Equal(0.70m, a.PurchaseWeight);
        Assert.Equal(0.50m, a.CurrentWeight);
        Assert.Equal(-0.20m, a.WeightDelta);
        Assert.Equal(0.30m, b.PurchaseWeight);
        Assert.Equal(0.50m, b.CurrentWeight);
        Assert.Equal(0.20m, b.WeightDelta);

        AllocationSliceDto purchaseA = result.PurchaseAllocation.Single(s => s.Key == "A");
        AllocationSliceDto purchaseB = result.PurchaseAllocation.Single(s => s.Key == "B");
        Assert.Equal(70m, purchaseA.ValueEur);
        Assert.Equal(0.70m, purchaseA.Weight);
        Assert.Equal(30m, purchaseB.ValueEur);
        Assert.Equal(0.30m, purchaseB.Weight);

        AllocationSliceDto currentA = result.CurrentAllocation.Single(s => s.Key == "A");
        Assert.Equal(10m, currentA.ValueEur);
        Assert.Equal(0.50m, currentA.Weight);
    }

    [Fact]
    public async Task GetOverviewAsync_ExcludesUnpricedPositions_FromTotalsAndWeights()
    {
        // Arrange: C has no cached quote, so it must not enter the totals nor the weight denominators.
        FakePfRepo pfRepo = new(new[] { Buy("A", 10, 70), Buy("C", 5, 50) });
        FakeMarketPriceService prices = new(new Dictionary<string, Money> { { "A", new Money(1m, "EUR") } });
        PortfolioOverviewQuery query = new(pfRepo, new FakeOptionRepo(), prices, new FakeConversionRepository());

        // Act
        PortfolioOverviewDto result = await query.GetOverviewAsync(AsOf);

        // Assert
        Assert.Equal(1, result.UnpricedPositionCount);
        Assert.Equal(70m, result.InvestedCostEur);
        Assert.Equal(10m, result.MarketValueEur);

        PortfolioPositionRowDto c = result.Positions.Single(p => p.Symbol == "C");
        Assert.False(c.IsPriced);
        Assert.Null(c.MarketValue);
        Assert.Null(c.CurrentWeight);
        Assert.Null(c.PurchaseWeight);

        Assert.Single(result.PurchaseAllocation);
        Assert.Single(result.CurrentAllocation);
    }

    [Fact]
    public async Task GetOverviewAsync_ExcludesClosedPositions()
    {
        // Arrange: CLOSED was fully sold, so its net quantity is zero and it must not appear.
        FakePfRepo pfRepo = new(new[] { Buy("A", 10, 70), Buy("CLOSED", 5, 10), Sell("CLOSED", 5, 10) });
        FakeMarketPriceService prices = new(new Dictionary<string, Money> { { "A", new Money(1m, "EUR") } });
        PortfolioOverviewQuery query = new(pfRepo, new FakeOptionRepo(), prices, new FakeConversionRepository());

        // Act
        PortfolioOverviewDto result = await query.GetOverviewAsync(AsOf);

        // Assert
        Assert.DoesNotContain(result.Positions, p => p.Symbol == "CLOSED");
        Assert.Single(result.Positions);
    }

    [Fact]
    public async Task GetOverviewAsync_DoesNotThrow_WhenPositionCostIsZero()
    {
        // Arrange: free stock (cost 0) must not throw nor produce a P/L percentage.
        FakePfRepo pfRepo = new(new[] { Buy("FREE", 5, 0) });
        FakeMarketPriceService prices = new(new Dictionary<string, Money> { { "FREE", new Money(2m, "EUR") } });
        PortfolioOverviewQuery query = new(pfRepo, new FakeOptionRepo(), prices, new FakeConversionRepository());

        // Act
        PortfolioOverviewDto result = await query.GetOverviewAsync(AsOf);

        // Assert
        Assert.Null(result.UnrealizedPnLPct);
        PortfolioPositionRowDto row = Assert.Single(result.Positions);
        Assert.True(row.IsPriced);
        Assert.Equal(10m, row.MarketValue);
        Assert.Null(row.UnrealizedPnLPct);
        Assert.Null(row.PurchaseWeight);
        Assert.Equal(1m, row.CurrentWeight);
        Assert.Empty(result.PurchaseAllocation);
    }

    [Fact]
    public async Task GetOverviewAsync_CapsNamedSlices_AndGroupsTheRestUnderOther()
    {
        // Arrange: 9 equal positions -> 8 named slices plus "Other" holding the residual.
        List<AssetTransaction> txs = Enumerable.Range(1, 9).Select(i => Buy($"T{i}", 1, 10)).ToList();
        Dictionary<string, Money> prices = Enumerable.Range(1, 9).ToDictionary(i => $"T{i}", _ => new Money(2m, "EUR"));
        FakePfRepo pfRepo = new(txs);
        PortfolioOverviewQuery query = new(pfRepo, new FakeOptionRepo(), new FakeMarketPriceService(prices), new FakeConversionRepository());

        // Act
        PortfolioOverviewDto result = await query.GetOverviewAsync(AsOf);

        // Assert
        Assert.Equal(9, result.PurchaseAllocation.Count);
        AllocationSliceDto other = result.PurchaseAllocation.Last();
        Assert.Equal("Other", other.Key);
        Assert.True(other.Weight > 0.10m && other.Weight < 0.12m);
        Assert.Equal(9, result.CurrentAllocation.Count);
        Assert.Equal("Other", result.CurrentAllocation.Last().Key);
    }

    [Fact]
    public async Task GetOverviewAsync_WeightsOfPricedPositions_SumToOne()
    {
        // Arrange
        List<AssetTransaction> txs = Enumerable.Range(1, 9).Select(i => Buy($"T{i}", 1, i * 5)).ToList();
        Dictionary<string, Money> prices = Enumerable.Range(1, 9).ToDictionary(i => $"T{i}", _ => new Money(2m, "EUR"));
        FakePfRepo pfRepo = new(txs);
        PortfolioOverviewQuery query = new(pfRepo, new FakeOptionRepo(), new FakeMarketPriceService(prices), new FakeConversionRepository());

        // Act
        PortfolioOverviewDto result = await query.GetOverviewAsync(AsOf);

        // Assert
        Assert.True(Math.Abs(1m - result.PurchaseAllocation.Sum(s => s.Weight)) < 0.01m);
        Assert.True(Math.Abs(1m - result.CurrentAllocation.Sum(s => s.Weight)) < 0.01m);
    }

    [Fact]
    public async Task GetOverviewAsync_UsesSameKeys_InBothCharts()
    {
        // Arrange
        FakePfRepo pfRepo = new(new[] { Buy("A", 10, 70), Buy("B", 10, 30), Buy("C", 10, 10) });
        FakeMarketPriceService prices = new(new Dictionary<string, Money>
        {
            { "A", new Money(1m, "EUR") },
            { "B", new Money(2m, "EUR") },
            { "C", new Money(3m, "EUR") },
        });
        PortfolioOverviewQuery query = new(pfRepo, new FakeOptionRepo(), prices, new FakeConversionRepository());

        // Act
        PortfolioOverviewDto result = await query.GetOverviewAsync(AsOf);

        // Assert
        HashSet<string> purchaseKeys = result.PurchaseAllocation.Select(s => s.Key).ToHashSet();
        HashSet<string> currentKeys = result.CurrentAllocation.Select(s => s.Key).ToHashSet();
        Assert.Equal(purchaseKeys, currentKeys);
        Assert.Equal(new[] { "A", "B", "C" }, purchaseKeys.OrderBy(k => k));
    }

    [Fact]
    public async Task GetOverviewAsync_ConvertsForeignCurrency_ToEur()
    {
        // Arrange: USD position converted with the default rate of 1.1 EUR per USD at 2025-01-01.
        FakePfRepo pfRepo = new(new[] { Buy("USD.X", 10, 110, "USD") });
        FakeMarketPriceService prices = new(new Dictionary<string, Money> { { "USD.X", new Money(1m, "USD") } });
        PortfolioOverviewQuery query = new(pfRepo, new FakeOptionRepo(), prices, new FakeConversionRepository());

        // Act
        PortfolioOverviewDto result = await query.GetOverviewAsync(AsOf);

        // Assert
        PortfolioPositionRowDto row = Assert.Single(result.Positions);
        Assert.Equal(100m, row.CostEur);
        Assert.Equal(9.09m, row.MarketValueEur);
        Assert.Equal(100m, result.InvestedCostEur);
        Assert.Equal(9.09m, result.MarketValueEur);
        Assert.Equal(-90.91m, result.UnrealizedPnLEur);
    }

    [Fact]
    public async Task GetOverviewAsync_ReportsOptionSymbols_AsUnsupported()
    {
        // Arrange
        FakePfRepo pfRepo = new(new[] { Buy("A", 10, 70) });
        FakeOptionRepo optionRepo = new();
        optionRepo.Add(new OptionTransaction(
            new Transaction(Guid.NewGuid(), DateTime.UtcNow.AddDays(-30), "Option", new Money(50, "EUR"), TransactionCategory.INCOME),
            "AAPL",
            "US0378331005",
            2,
            AssetTransactionType.Buy));
        FakeMarketPriceService prices = new(new Dictionary<string, Money> { { "A", new Money(1m, "EUR") } });
        PortfolioOverviewQuery query = new(pfRepo, optionRepo, prices, new FakeConversionRepository());

        // Act
        PortfolioOverviewDto result = await query.GetOverviewAsync(AsOf);

        // Assert
        Assert.Equal(1, result.OptionSymbolCount);
    }

    [Fact]
    public async Task GetOverviewAsync_IncludesLongOptionPosition()
    {
        FakePfRepo pfRepo = new();
        FakeOptionRepo optionRepo = new();
        optionRepo.Add(Option("VET", "EUR", 2, 30m, AssetTransactionType.Buy));
        FakeMarketPriceService prices = new(new Dictionary<string, Money> { { "VET", new Money(35m, "EUR") } });
        PortfolioOverviewQuery query = new(pfRepo, optionRepo, prices, new FakeConversionRepository());

        PortfolioOverviewDto result = await query.GetOverviewAsync(AsOf);

        PortfolioPositionRowDto row = Assert.Single(result.Positions);
        Assert.Equal("Option", row.AssetClass);
        Assert.Equal(2m, row.Quantity);
        Assert.Equal(60m, row.Cost);
        Assert.Equal(70m, row.MarketValue);
        Assert.Equal(10m, row.UnrealizedPnL);
        Assert.True(row.IsPriced);
        Assert.Equal(70m, result.MarketValueEur);
        Assert.Equal(60m, result.InvestedCostEur);
        Assert.Equal(10m, result.UnrealizedPnLEur);
        Assert.Equal(1, result.OptionSymbolCount);
    }

    [Fact]
    public async Task GetOverviewAsync_IncludesShortOptionPosition_ExcludesFromPies()
    {
        FakePfRepo pfRepo = new();
        FakeOptionRepo optionRepo = new();
        // Open short 3 options at 100 premium each (credit 300), current price 80 (gain).
        optionRepo.Add(Option("SPX", "EUR", 3, 100m, AssetTransactionType.Sell));
        FakeMarketPriceService prices = new(new Dictionary<string, Money> { { "SPX", new Money(80m, "EUR") } });
        PortfolioOverviewQuery query = new(pfRepo, optionRepo, prices, new FakeConversionRepository());

        PortfolioOverviewDto result = await query.GetOverviewAsync(AsOf);

        PortfolioPositionRowDto row = Assert.Single(result.Positions);
        Assert.Equal("Option", row.AssetClass);
        Assert.Equal(-3m, row.Quantity);
        Assert.Equal(-300m, row.Cost);
        Assert.Equal(-240m, row.MarketValue);
        Assert.Equal(60m, row.UnrealizedPnL);
        Assert.True(row.IsPriced);
        Assert.Equal(-240m, result.MarketValueEur);
        Assert.Equal(-300m, result.InvestedCostEur);
        Assert.Equal(60m, result.UnrealizedPnLEur);
        Assert.Empty(result.PurchaseAllocation);
        Assert.Empty(result.CurrentAllocation);
    }

    [Fact]
    public async Task GetOverviewAsync_UnpricedOption_CountedButNotInTotalsOrPies()
    {
        FakePfRepo pfRepo = new();
        FakeOptionRepo optionRepo = new();
        optionRepo.Add(Option("VET", "EUR", 2, 30m, AssetTransactionType.Buy));
        FakeMarketPriceService prices = new();
        PortfolioOverviewQuery query = new(pfRepo, optionRepo, prices, new FakeConversionRepository());

        PortfolioOverviewDto result = await query.GetOverviewAsync(AsOf);

        Assert.Equal(1, result.UnpricedPositionCount);
        PortfolioPositionRowDto row = Assert.Single(result.Positions);
        Assert.False(row.IsPriced);
        Assert.Null(row.MarketValue);
        Assert.Null(row.PurchaseWeight);
        Assert.Null(row.CurrentWeight);
        Assert.Empty(result.PurchaseAllocation);
        Assert.Empty(result.CurrentAllocation);
    }

    [Fact]
    public async Task GetOverviewAsync_ExcludesClosedOptionPosition()
    {
        FakePfRepo pfRepo = new();
        FakeOptionRepo optionRepo = new();
        optionRepo.Add(Option("VET", "EUR", 2, 30m, AssetTransactionType.Buy));
        optionRepo.Add(Option("VET", "EUR", 2, 40m, AssetTransactionType.Sell));
        PortfolioOverviewQuery query = new(pfRepo, optionRepo, new FakeMarketPriceService(new Dictionary<string, Money> { { "VET", new Money(35m, "EUR") } }), new FakeConversionRepository());

        PortfolioOverviewDto result = await query.GetOverviewAsync(AsOf);

        Assert.Empty(result.Positions);
        Assert.Equal(0, result.OptionSymbolCount);
    }

    [Fact]
    public async Task GetOverviewAsync_MergesStockAndOptionSameSymbol_AsMixed()
    {
        FakePfRepo pfRepo = new(new[] { Buy("VET", 10, 500) });
        FakeOptionRepo optionRepo = new();
        optionRepo.Add(Option("VET", "EUR", 3, 100m, AssetTransactionType.Sell));
        FakeMarketPriceService prices = new(new Dictionary<string, Money> { { "VET", new Money(60m, "EUR") } });
        PortfolioOverviewQuery query = new(pfRepo, optionRepo, prices, new FakeConversionRepository());

        PortfolioOverviewDto result = await query.GetOverviewAsync(AsOf);

        PortfolioPositionRowDto row = Assert.Single(result.Positions);
        Assert.Equal("Mixed", row.AssetClass);
        Assert.Equal(7m, row.Quantity); // 10 stock - 3 short options
        Assert.Equal(200m, row.Cost); // 500 stock - 300 option credit
        Assert.Equal(420m, row.MarketValue);
        Assert.Equal(220m, row.UnrealizedPnL);
        Assert.Equal(1, result.OptionSymbolCount);
    }

    private static OptionTransaction Option(string symbol, string currency, decimal quantity, decimal premium, AssetTransactionType type)
    {
        TransactionCategory category = type == AssetTransactionType.Buy
            ? TransactionCategory.INVESTMENT
            : TransactionCategory.DIVESTMENT;
        Transaction tx = new(Guid.NewGuid(), DateTime.UtcNow.AddDays(-30), $"Opt {symbol}", new Money(premium * quantity, currency), category);
        return new OptionTransaction(tx, symbol, "ISIN", quantity, type);
    }

    private static AssetTransaction Buy(string symbol, decimal quantity, decimal amount, string currency = "EUR") =>
        new(new Transaction(Guid.NewGuid(), DateTime.UtcNow.AddDays(-30), $"Buy {symbol}", new Money(amount, currency), TransactionCategory.INCOME), symbol, quantity, AssetTransactionType.Buy);

    private static AssetTransaction Sell(string symbol, decimal quantity, decimal amount) =>
        new(new Transaction(Guid.NewGuid(), DateTime.UtcNow.AddDays(-1), $"Sell {symbol}", new Money(amount, "EUR"), TransactionCategory.INCOME), symbol, quantity, AssetTransactionType.Sell);

    private sealed class FakePfRepo : IPortfolioRepository
    {
        private readonly List<AssetTransaction> _transactions;

        public FakePfRepo(IEnumerable<AssetTransaction> txs) => this._transactions = txs.ToList();

        public FakePfRepo()
            : this(Array.Empty<AssetTransaction>())
        {
        }

        public void AddOrUpdate(AssetTransaction tx) => this._transactions.Add(tx);

        public IEnumerable<AssetTransaction> GetAssetTransactions(string symbol) =>
            this._transactions.Where(t => t.Symbol == symbol);

        public IEnumerable<AssetTransaction> GetAllTransactions() => this._transactions;

        public void Initialize(IEnumerable<AssetTransaction> transactions)
        {
            this._transactions.Clear();
            this._transactions.AddRange(transactions);
        }

        public bool Delete(Guid transactionId) => true;

        public int DeleteByYear(int year) => this._transactions.RemoveAll(t => t.Transaction.Date.Year == year);
    }

    private sealed class FakeOptionRepo : IOptionTransactionRepository
    {
        private readonly List<OptionTransaction> _options = new();

        public IEnumerable<OptionTransaction> GetAll() => this._options;

        public void Add(OptionTransaction transaction) => this._options.Add(transaction);

        public void Update(OptionTransaction transaction)
        {
        }

        public bool Delete(Guid id) => true;

        public int DeleteByYear(int year) => this._options.RemoveAll(o => o.Transaction.Date.Year == year);

        public void Initialize(IEnumerable<OptionTransaction> transactions)
        {
            this._options.Clear();
            this._options.AddRange(transactions);
        }
    }
}
