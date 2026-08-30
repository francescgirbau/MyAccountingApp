using MyAccountingApp.Application.DTOs;
using MyAccountingApp.Application.Interfaces;
using MyAccountingApp.Application.Services;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Interfaces;
using MyAccountingApp.Domain.ValueObjects;

namespace MyAccountingApp.Application.Tests.Services;

public class DashboardQueryTests
{
    private static readonly DateOnly AsOf = new(2026, 8, 11);

    [Fact]
    public async Task GetAsync_ShouldComputeCashYtdAndMtd()
    {
        FakeTxRepo txRepo = new();
        txRepo.Add(new Transaction(new DateTime(2026, 1, 10), "Salary", new Money(1000, "EUR"), TransactionCategory.INCOME));
        txRepo.Add(new Transaction(new DateTime(2026, 1, 15), "Rent", new Money(400, "EUR"), TransactionCategory.EXPENSE));
        txRepo.Add(new Transaction(new DateTime(2026, 1, 20), "Transfer", new Money(200, "EUR"), TransactionCategory.TRANSFER));
        txRepo.Add(new Transaction(new DateTime(2026, 8, 1), "Bonus", new Money(500, "EUR"), TransactionCategory.INCOME));
        txRepo.Add(new Transaction(new DateTime(2026, 8, 5), "Groceries", new Money(100, "EUR"), TransactionCategory.EXPENSE));
        txRepo.Add(new Transaction(new DateTime(2025, 12, 30), "Old", new Money(999, "EUR"), TransactionCategory.INCOME));
        DashboardQuery query = new(txRepo, new FakePfRepo(), new FakeValidationQuery());

        DashboardDto dashboard = await query.GetAsync(AsOf);

        Assert.Equal(1500, dashboard.Cash.OperatingYtd.Income);
        Assert.Equal(500, dashboard.Cash.OperatingYtd.Expenses);
        Assert.Equal(1000, dashboard.Cash.OperatingYtd.NetOperatingCashFlow);
        Assert.Equal(200, dashboard.Cash.InternalYtd.Transfers);
        Assert.Equal(0, dashboard.Cash.InternalYtd.Deposits);
        Assert.Equal(500, dashboard.Cash.OperatingMtd.Income);
        Assert.Equal(100, dashboard.Cash.OperatingMtd.Expenses);
        Assert.Equal(400, dashboard.Cash.OperatingMtd.NetOperatingCashFlow);
    }

    [Fact]
    public async Task GetAsync_ShouldComputeFifoPortfolioTotals()
    {
        FakePfRepo pfRepo = new();
        pfRepo.Add(CreateAsset("AAPL", new DateTime(2026, 1, 5), 10, 1000, AssetTransactionType.Buy));
        pfRepo.Add(CreateAsset("AAPL", new DateTime(2026, 8, 3), 2, 300, AssetTransactionType.Sell));
        DashboardQuery query = new(new FakeTxRepo(), pfRepo, new FakeValidationQuery());

        DashboardDto dashboard = await query.GetAsync(AsOf);

        Assert.Equal(800, dashboard.Portfolio.TotalCostBasisEur);
        Assert.Equal(100, dashboard.Portfolio.RealizedGainLossYtdEur);
        Assert.Equal(1, dashboard.Portfolio.OpenPositionCount);
        Assert.Equal(1, dashboard.Portfolio.SymbolCount);
        Assert.Null(dashboard.Portfolio.TotalMarketValueEur);
        Assert.Equal(1000, dashboard.Cash.InvestingYtd.Purchases);
        Assert.Equal(300, dashboard.Cash.InvestingYtd.Sales);
        Assert.Equal(-700, dashboard.Cash.InvestingYtd.NetInvestedCash);
    }

    [Fact]
    public async Task GetAsync_ShouldOnlyCountInvestingWithinYtd()
    {
        FakePfRepo pfRepo = new();
        pfRepo.Add(CreateAsset("MSFT", new DateTime(2025, 11, 10), 5, 500, AssetTransactionType.Buy));
        pfRepo.Add(CreateAsset("MSFT", new DateTime(2026, 3, 2), 3, 350, AssetTransactionType.Buy));
        pfRepo.Add(CreateAsset("MSFT", new DateTime(2026, 7, 15), 1, 120, AssetTransactionType.Sell));
        DashboardQuery query = new(new FakeTxRepo(), pfRepo, new FakeValidationQuery());

        DashboardDto dashboard = await query.GetAsync(AsOf);

        Assert.Equal(350, dashboard.Cash.InvestingYtd.Purchases);
        Assert.Equal(120, dashboard.Cash.InvestingYtd.Sales);
        Assert.Equal(-230, dashboard.Cash.InvestingYtd.NetInvestedCash);
    }

    [Fact]
    public async Task GetAsync_ShouldAlert_WhenNonEurMovementsExist()
    {
        FakeTxRepo txRepo = new();
        txRepo.Add(new Transaction(new DateTime(2026, 1, 10), "USD income", new Money(100, "USD"), TransactionCategory.INCOME));
        DashboardQuery query = new(txRepo, new FakePfRepo(), new FakeValidationQuery());

        DashboardDto dashboard = await query.GetAsync(AsOf);

        DashboardAlertDto alert = Assert.Single(dashboard.Alerts);
        Assert.Equal("UNCONVERTED_CURRENCY", alert.Code);
        Assert.Equal("/conversions", alert.Link);
    }

    [Fact]
    public async Task GetAsync_ExcludesFxLegsFromTransfersAndDeposits()
    {
        Guid pairId = Guid.NewGuid();
        FakeTxRepo txRepo = new();
        txRepo.Add(CreateFxLeg(new DateTime(2026, 2, 1), "FX out", 490.24m, "EUR", pairId, FxLeg.Out));
        txRepo.Add(CreateFxLeg(new DateTime(2026, 2, 1), "FX in", 545.20m, "USD", pairId, FxLeg.In));
        txRepo.Add(new Transaction(new DateTime(2026, 2, 2), "Transfer", new Money(200, "EUR"), TransactionCategory.TRANSFER));
        txRepo.Add(new Transaction(new DateTime(2026, 2, 2), "Deposit", new Money(200, "EUR"), TransactionCategory.DEPOSIT));
        DashboardQuery query = new(txRepo, new FakePfRepo(), new FakeValidationQuery());

        DashboardDto dashboard = await query.GetAsync(AsOf);

        Assert.Equal(200, dashboard.Cash.InternalYtd.Transfers);
        Assert.Equal(200, dashboard.Cash.InternalYtd.Deposits);
        Assert.Equal(490.24m, dashboard.Cash.InternalYtd.FxOut);
        Assert.Equal(0, dashboard.Cash.InternalYtd.FxIn);
        Assert.Equal(-490.24m, dashboard.Cash.InternalYtd.FxNet);
    }

    private static AssetTransaction CreateAsset(string symbol, DateTime date, decimal quantity, decimal amount, AssetTransactionType type)
    {
        Transaction transaction = new(date, "Test " + symbol, new Money(amount, "EUR"), TransactionCategory.EXPENSE);
        return new AssetTransaction(transaction, symbol, quantity, type);
    }

    private static Transaction CreateFxLeg(DateTime date, string description, decimal amount, string currency, Guid pairId, FxLeg leg)
    {
        Transaction transaction = new(date, description, new Money(amount, currency), TransactionCategory.FX_CONVERSION);
        transaction.SetFxPair(pairId, leg);
        return transaction;
    }

    private sealed class FakeTxRepo : ITransactionRepository
    {
        private readonly List<Transaction> _transactions = new();

        public void Add(Transaction transaction) => this._transactions.Add(transaction);
        public void Initialize(IEnumerable<Transaction> transactions) => this._transactions.Clear();
        public void AddOrUpdate(Transaction transaction) => this._transactions.Add(transaction);
        public IEnumerable<Transaction> GetAll() => this._transactions;
        public bool Delete(Transaction transaction) => this._transactions.Remove(transaction);
        public int DeleteByYear(int year) => this._transactions.RemoveAll(t => t.Date.Year == year);
    }

    private sealed class FakePfRepo : IPortfolioRepository
    {
        private readonly List<AssetTransaction> _transactions = new();

        public void Add(AssetTransaction transaction) => this._transactions.Add(transaction);
        public void AddOrUpdate(AssetTransaction assetTransaction) => this._transactions.Add(assetTransaction);
        public IEnumerable<AssetTransaction> GetAssetTransactions(string symbol) =>
            this._transactions.Where(t => t.Symbol == symbol);
        public IEnumerable<AssetTransaction> GetAllTransactions() => this._transactions;
        public bool Delete(Guid transactionId) => this._transactions.RemoveAll(t => t.Transaction.Id == transactionId) > 0;
        public void Initialize(IEnumerable<AssetTransaction> transactions) => this._transactions.Clear();
        public int DeleteByYear(int year) => this._transactions.RemoveAll(t => t.Transaction.Date.Year == year);
    }

    [Fact]
    public async Task GetAsync_ShouldAddDataQualityAlert_WhenValidationHasErrors()
    {
        FakeTxRepo txRepo = new();
        FakeValidationQuery validation = new(1, 0);

        DashboardQuery query = new(txRepo, new FakePfRepo(), validation);

        DashboardDto dashboard = await query.GetAsync(AsOf);

        DashboardAlertDto alert = Assert.Single(dashboard.Alerts);
        Assert.Equal("error", alert.Severity);
        Assert.Equal("DATA_QUALITY", alert.Code);
        Assert.Equal("/data-quality", alert.Link);
    }

    [Fact]
    public async Task GetAsync_ShouldAddDataQualityAlert_WhenValidationHasWarnings()
    {
        FakeTxRepo txRepo = new();
        FakeValidationQuery validation = new(0, 2);

        DashboardQuery query = new(txRepo, new FakePfRepo(), validation);

        DashboardDto dashboard = await query.GetAsync(AsOf);

        DashboardAlertDto alert = Assert.Single(dashboard.Alerts);
        Assert.Equal("warning", alert.Severity);
        Assert.Equal("DATA_QUALITY", alert.Code);
    }

    [Fact]
    public async Task GetAsync_ShouldNotAddDataQualityAlert_WhenValidationClean()
    {
        FakeTxRepo txRepo = new();
        FakeValidationQuery validation = new(0, 0);

        DashboardQuery query = new(txRepo, new FakePfRepo(), validation);

        DashboardDto dashboard = await query.GetAsync(AsOf);

        Assert.DoesNotContain(dashboard.Alerts, a => a.Code == "DATA_QUALITY");
    }

    private sealed class FakeValidationQuery : IValidationQuery
    {
        private readonly int _errorCount;
        private readonly int _warningCount;

        public FakeValidationQuery(int errorCount = 0, int warningCount = 0)
        {
            this._errorCount = errorCount;
            this._warningCount = warningCount;
        }

        public ValidationResult ValidateAll() => new ValidationResult(
            this._errorCount == 0,
            Enumerable.Range(0, this._errorCount).Select(_ => new ValidationError("FIELD", "error", "error")).ToList(),
            Enumerable.Range(0, this._warningCount).Select(_ => new ValidationError("FIELD", "warning", "warning")).ToList());
    }
}