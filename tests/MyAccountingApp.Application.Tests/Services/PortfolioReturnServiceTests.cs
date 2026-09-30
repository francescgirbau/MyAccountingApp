using MyAccountingApp.Application.Services;
using MyAccountingApp.Contracts;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Interfaces;
using MyAccountingApp.Domain.ValueObjects;
using MyAccountingApp.TestUtilities.Fakes;

namespace MyAccountingApp.Application.Tests.Services;

public class PortfolioReturnServiceTests
{
    [Fact]
    public async Task GetReturnAsync_EuropeanBuy_ComputesTotalAndAnnualized()
    {
        FakePortfolioRepo repo = new();
        repo.AddOrUpdate(Buy("BMW.DE", "EUR", 80m, 10, new DateTime(2024, 1, 1)));
        PortfolioReturnService service = CreateService(repo);

        PortfolioReturnDto result = await service.GetReturnAsync(new DateOnly(2026, 1, 1));

        Assert.Equal(800m, result.CapitalInvestedEur);
        Assert.Equal(0m, result.TotalProceedsEur);
        Assert.Equal(807.50m, result.TerminalValueEur);
        Assert.Equal(0.0094m, result.TotalReturn);                     // (807.50 - 800) / 800
        Assert.NotNull(result.AnnualizedReturn);
        Assert.Equal(2m, result.AyiYears!.Value, 1);
        Assert.Equal(0, result.ExcludedFlowCount);
    }

    [Fact]
    public async Task GetReturnAsync_ConvertsHistoricalFlows_AtTheirOwnDate()
    {
        FakePortfolioRepo repo = new();
        repo.AddOrUpdate(Buy("AAPL", "USD", 100m, 10, new DateTime(2020, 1, 1)));
        PortfolioReturnService service = CreateService(repo);

        PortfolioReturnDto result = await service.GetReturnAsync(new DateOnly(2026, 1, 1));

        Assert.Equal(909.09m, result.CapitalInvestedEur);              // 1000 USD / 1.1
        Assert.Equal(1365.91m, result.TerminalValueEur);               // 10 * 150.25 USD / 1.1
        Assert.Equal(0.5025m, result.TotalReturn!.Value, 4);
        Assert.Equal(0, result.ExcludedFlowCount);
    }

    [Fact]
    public async Task GetReturnAsync_AddsDividendsNetOfWithholdingTaxes()
    {
        FakePortfolioRepo repo = new();
        repo.AddOrUpdate(Buy("BMW.DE", "EUR", 80m, 10, new DateTime(2024, 1, 1)));
        FakeTxRepo txRepo = new();
        txRepo.AddOrUpdate(Cash(new DateTime(2024, 6, 1), 50m, TransactionCategory.DIVIDEND));
        txRepo.AddOrUpdate(Cash(new DateTime(2024, 6, 1), 10m, TransactionCategory.WITHHOLDING_TAX));
        PortfolioReturnService service = CreateService(repo, txRepo: txRepo);

        PortfolioReturnDto result = await service.GetReturnAsync(new DateOnly(2026, 1, 1));

        Assert.Equal(40m, result.TotalProceedsEur);                    // 50 - 10
        Assert.Equal(0.0594m, result.TotalReturn);                     // (40 + 807.50 - 800) / 800
    }

    [Fact]
    public async Task GetReturnAsync_CountsSaleProceeds_AndReducesTerminalValue()
    {
        FakePortfolioRepo repo = new();
        repo.AddOrUpdate(Buy("BMW.DE", "EUR", 80m, 10, new DateTime(2024, 1, 1)));
        repo.AddOrUpdate(Sell("BMW.DE", "EUR", 90m, 5, new DateTime(2025, 6, 1)));
        PortfolioReturnService service = CreateService(repo);

        PortfolioReturnDto result = await service.GetReturnAsync(new DateOnly(2026, 1, 1));

        Assert.Equal(450m, result.TotalProceedsEur);                   // 5 * 90
        Assert.Equal(403.75m, result.TerminalValueEur);                // remaining 5 * 80.75
        Assert.Equal(0.0672m, result.TotalReturn);                     // (450 + 403.75 - 800) / 800
    }

    [Fact]
    public async Task GetReturnAsync_IgnoresCorporateActions()
    {
        FakePortfolioRepo repo = new();
        repo.AddOrUpdate(Buy("BMW.DE", "EUR", 80m, 10, new DateTime(2024, 1, 1)));
        repo.AddOrUpdate(new AssetTransaction(
            new Transaction(new DateTime(2024, 2, 1), "Spin-off", new Money(9999m, "EUR"), TransactionCategory.INVESTMENT),
            "BMW.DE",
            5m,
            AssetTransactionType.CorporateAction));
        PortfolioReturnService service = CreateService(repo);

        PortfolioReturnDto result = await service.GetReturnAsync(new DateOnly(2026, 1, 1));

        Assert.Equal(800m, result.CapitalInvestedEur);
        Assert.Equal(0m, result.TotalProceedsEur);
    }

    [Fact]
    public async Task GetReturnAsync_ExcludesFlowsWithoutHistoricalRate()
    {
        FakePortfolioRepo repo = new();
        repo.AddOrUpdate(Buy("TOYOTA", "JPY", 100m, 100, new DateTime(2024, 1, 1)));
        PortfolioReturnService service = CreateService(repo);

        PortfolioReturnDto result = await service.GetReturnAsync(new DateOnly(2026, 1, 1));

        Assert.Equal(1, result.ExcludedFlowCount);
        Assert.Equal(0m, result.CapitalInvestedEur);
        Assert.Null(result.TotalReturn);
        Assert.Null(result.AnnualizedReturn);
        Assert.Null(result.AyiYears);
    }

    [Fact]
    public async Task GetReturnAsync_CountsOptionBuys_AsCapital()
    {
        FakePortfolioRepo repo = new();
        FakeOptionRepository optionRepo = new();
        optionRepo.Add(new OptionTransaction(
            new Transaction(new DateTime(2024, 1, 1), "Buy SPX", new Money(500m, "EUR"), TransactionCategory.EXPENSE),
            "SPX",
            "ISIN",
            2,
            AssetTransactionType.Buy));
        FakeMarketPriceService priceService = new(new Dictionary<string, Money> { { "SPX", new Money(300m, "EUR") } });
        PortfolioReturnService service = CreateService(repo, optionRepo, priceService: priceService);

        PortfolioReturnDto result = await service.GetReturnAsync(new DateOnly(2026, 1, 1));

        Assert.Equal(500m, result.CapitalInvestedEur);
        Assert.Equal(600m, result.TerminalValueEur);                   // 2 * 300
        Assert.Equal(0.20m, result.TotalReturn);                       // (600 - 500) / 500
    }

    private static AssetTransaction Buy(string symbol, string currency, decimal price, decimal quantity, DateTime date)
    {
        return new AssetTransaction(
            new Transaction(Guid.NewGuid(), date, $"Buy {symbol}", new Money(price * quantity, currency), TransactionCategory.EXPENSE),
            symbol,
            quantity,
            AssetTransactionType.Buy);
    }

    private static AssetTransaction Sell(string symbol, string currency, decimal price, decimal quantity, DateTime date)
    {
        return new AssetTransaction(
            new Transaction(Guid.NewGuid(), date, $"Sell {symbol}", new Money(price * quantity, currency), TransactionCategory.DIVESTMENT),
            symbol,
            quantity,
            AssetTransactionType.Sell);
    }

    private static Transaction Cash(DateTime date, decimal amount, TransactionCategory category)
    {
        return new Transaction(Guid.NewGuid(), date, category.ToString(), new Money(amount, "EUR"), category);
    }

    private static PortfolioReturnService CreateService(
        FakePortfolioRepo repo,
        FakeOptionRepository? optionRepo = null,
        FakeTxRepo? txRepo = null,
        FakeMarketPriceService? priceService = null)
    {
        optionRepo ??= new FakeOptionRepository();
        txRepo ??= new FakeTxRepo();
        priceService ??= new FakeMarketPriceService();
        PositionEngine engine = new(repo, optionRepo, priceService);
        FakeConversionRepository conversions = new();
        FakeApiQuotaManager quota = new();
        FakePendingWorkQueue queue = new();
        CurrencyRateService rateService = new(conversions, new FakeCurrencyConverter(), Currencies.EUR, quota, queue);
        ToEurConverter converter = new(rateService);
        PositionValuationService valuationService = new(repo, optionRepo, engine, converter);
        return new PortfolioReturnService(repo, optionRepo, txRepo, converter, valuationService);
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

    private sealed class FakeTxRepo : ITransactionRepository
    {
        private readonly List<Transaction> _transactions = new();

        public void Initialize(IEnumerable<Transaction> transactions)
        {
            this._transactions.Clear();
            this._transactions.AddRange(transactions);
        }

        public void AddOrUpdate(Transaction transaction) => this._transactions.Add(transaction);

        public IEnumerable<Transaction> GetAll() => this._transactions;

        public bool Delete(Transaction transaction) => this._transactions.Remove(transaction);

        public int DeleteByYear(int year) => this._transactions.RemoveAll(t => t.Date.Year == year);
    }
}