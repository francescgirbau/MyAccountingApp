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

        Assert.Equal(1500, dashboard.Cash.IncomeYtd);
        Assert.Equal(500, dashboard.Cash.ExpenseYtd);
        Assert.Equal(1000, dashboard.Cash.NetCashFlowYtd);
        Assert.Equal(200, dashboard.Cash.TransfersYtd);
        Assert.Equal(0, dashboard.Cash.DepositsYtd);
        Assert.Equal(500, dashboard.Cash.IncomeMtd);
        Assert.Equal(100, dashboard.Cash.ExpenseMtd);
        Assert.Equal(400, dashboard.Cash.NetCashFlowMtd);
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

    private static AssetTransaction CreateAsset(string symbol, DateTime date, decimal quantity, decimal amount, AssetTransactionType type)
    {
        Transaction transaction = new(date, "Test " + symbol, new Money(amount, "EUR"), TransactionCategory.EXPENSE);
        return new AssetTransaction(transaction, symbol, quantity, type);
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

    private sealed class FakeValidationQuery : IValidationQuery
    {
        public ValidationResult ValidateAll() => new ValidationResult(true, new List<ValidationError>(), new List<ValidationError>());
    }
}