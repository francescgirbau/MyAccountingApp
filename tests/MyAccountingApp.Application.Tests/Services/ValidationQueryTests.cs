using MyAccountingApp.Application.Interfaces;
using MyAccountingApp.Application.Services;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Interfaces;
using MyAccountingApp.Domain.ValueObjects;

namespace MyAccountingApp.Application.Tests.Services;

public class ValidationQueryTests
{
    [Fact]
    public void ValidateAll_ReturnsValid_WhenNoTransactions()
    {
        FakeTxRepo txRepo = new();
        FakePfRepo pfRepo = new();
        TransactionValidator validator = new();
        ValidationQuery query = new(txRepo, pfRepo, validator);

        ValidationResult result = query.ValidateAll();

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void ValidateAll_CollectsErrors_FromBothRepositories()
    {
        FakeTxRepo txRepo = new();
        FakePfRepo pfRepo = new();

        Transaction invalidTx = new(
            Guid.NewGuid(),
            DateTime.UtcNow.AddDays(10),
            "",
            new Money(100, "EUR"),
            TransactionCategory.INCOME);
        txRepo.AddOrUpdate(invalidTx);

        Transaction txForAsset = new(
            Guid.NewGuid(),
            DateTime.UtcNow,
            "Valid",
            new Money(100, "EUR"),
            TransactionCategory.EXPENSE);
        AssetTransaction invalidAsset = new(txForAsset, "", 5, AssetTransactionType.Buy);
        pfRepo.AddOrUpdate(invalidAsset);

        TransactionValidator validator = new();
        ValidationQuery query = new(txRepo, pfRepo, validator);

        ValidationResult result = query.ValidateAll();

        Assert.False(result.IsValid);
        Assert.Equal(3, result.Errors.Count);
    }

    private sealed class FakeTxRepo : ITransactionRepository
    {
        private readonly List<Transaction> _transactions = new();

        public void AddOrUpdate(Transaction transaction) => this._transactions.Add(transaction);
        public void Initialize(IEnumerable<Transaction> transactions)
        {
            this._transactions.Clear();
            this._transactions.AddRange(transactions);
        }

        public IEnumerable<Transaction> GetAll() => this._transactions;
        public bool Delete(Transaction transaction) => this._transactions.Remove(transaction);
    }

    private sealed class FakePfRepo : IPortfolioRepository
    {
        private readonly List<AssetTransaction> _transactions = new();

        public void AddOrUpdate(AssetTransaction tx) => this._transactions.Add(tx);
        public IEnumerable<AssetTransaction> GetAssetTransactions(string symbol) =>
            this._transactions.Where(t => t.Symbol == symbol);
        public IEnumerable<AssetTransaction> GetAllTransactions() => this._transactions;
        public void Initialize(IEnumerable<AssetTransaction> transactions)
        {
            this._transactions.Clear();
            this._transactions.AddRange(transactions);
        }
    }
}
