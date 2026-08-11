using MyAccountingApp.Application.DTOs;
using MyAccountingApp.Application.Services;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Interfaces;
using MyAccountingApp.Domain.ValueObjects;
using MyAccountingApp.TestUtilities.Fakes;

namespace MyAccountingApp.Application.Tests.Services;

public class PositionValuationServiceTests
{
    private static AssetTransaction Buy(string symbol, string currency, decimal price, decimal quantity, DateTime date)
    {
        Money money = new(price * quantity, currency);
        Transaction tx = new(
            Guid.NewGuid(),
            date,
            $"Buy {symbol}",
            money,
            TransactionCategory.EXPENSE);
        return new AssetTransaction(tx, symbol, quantity, AssetTransactionType.Buy);
    }

    private static PositionValuationService CreateService(FakePortfolioRepo repo, FakeApiQuotaManager quota)
    {
        FakeMarketPriceService priceService = new();
        PositionEngine engine = new(repo, priceService);
        FakeConversionRepository conversionRepo = new();
        FakePendingConversionQueue queue = new();
        CurrencyRateService rateService = new(conversionRepo, new FakeCurrencyConverter(), Currencies.EUR, quota, queue);
        ToEurConverter converter = new(rateService);
        return new PositionValuationService(repo, engine, converter);
    }

    [Fact]
    public async Task GetValuationsAsync_ConvertsUsdPosition_WithRateAndRateDate()
    {
        FakePortfolioRepo repo = new();
        repo.AddOrUpdate(Buy("AAPL", "USD", 150, 10, new DateTime(2024, 1, 15)));
        PositionValuationService service = CreateService(repo, new FakeApiQuotaManager());

        IReadOnlyList<PositionValuationDto> result = await service.GetValuationsAsync(new DateOnly(2023, 12, 1));

        PositionValuationDto valuation = Assert.Single(result);
        Assert.Equal("AAPL", valuation.Symbol);
        Assert.Equal("USD", valuation.Currency);
        Assert.Equal(10, valuation.NetQuantity);
        Assert.Equal(150.25m, valuation.MarketPrice);
        Assert.Equal(1365.91m, valuation.ValueEur);
        Assert.Equal(2.50m, valuation.UnrealizedGainLoss);
        Assert.Equal(2.27m, valuation.UnrealizedGainLossEur);
        Assert.Equal(1.1m, valuation.Rate);
        Assert.Equal(new DateOnly(2023, 12, 1), valuation.RateDate);
        Assert.False(valuation.IsStale);
    }

    [Fact]
    public async Task GetValuationsAsync_EurPosition_UsesIdentityRate()
    {
        FakePortfolioRepo repo = new();
        repo.AddOrUpdate(Buy("BMW.DE", "EUR", 80.75m, 10, new DateTime(2024, 1, 15)));
        PositionValuationService service = CreateService(repo, new FakeApiQuotaManager());

        IReadOnlyList<PositionValuationDto> result = await service.GetValuationsAsync(new DateOnly(2023, 12, 1));

        PositionValuationDto valuation = Assert.Single(result);
        Assert.Equal("EUR", valuation.Currency);
        Assert.Equal(807.50m, valuation.ValueEur);
        Assert.Equal(1m, valuation.Rate);
        Assert.False(valuation.IsStale);
    }

    [Fact]
    public async Task GetValuationsAsync_MarksStale_WhenRateIsFallback()
    {
        FakePortfolioRepo repo = new();
        repo.AddOrUpdate(Buy("AAPL", "USD", 150, 10, new DateTime(2024, 1, 15)));
        PositionValuationService service = CreateService(repo, new FakeApiQuotaManager() { CanConsumeResult = false });

        IReadOnlyList<PositionValuationDto> result = await service.GetValuationsAsync(new DateOnly(2023, 12, 1));

        PositionValuationDto valuation = Assert.Single(result);
        Assert.True(valuation.IsStale);
        Assert.Equal(new DateOnly(2005, 12, 1), valuation.RateDate);
    }

    [Fact]
    public async Task GetValuationsAsync_LeavesEurNull_WhenNoMarketPrice()
    {
        FakePortfolioRepo repo = new();
        repo.AddOrUpdate(Buy("UNKNOWN", "USD", 10, 5, new DateTime(2024, 1, 15)));
        PositionValuationService service = CreateService(repo, new FakeApiQuotaManager());

        IReadOnlyList<PositionValuationDto> result = await service.GetValuationsAsync(new DateOnly(2023, 12, 1));

        PositionValuationDto valuation = Assert.Single(result);
        Assert.Null(valuation.MarketPrice);
        Assert.Null(valuation.ValueEur);
        Assert.Null(valuation.Rate);
    }

    [Fact]
    public async Task GetValuationsAsync_ReturnsEmpty_WhenNoTransactions()
    {
        FakePortfolioRepo repo = new();
        PositionValuationService service = CreateService(repo, new FakeApiQuotaManager());

        IReadOnlyList<PositionValuationDto> result = await service.GetValuationsAsync(new DateOnly(2023, 12, 1));

        Assert.Empty(result);
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