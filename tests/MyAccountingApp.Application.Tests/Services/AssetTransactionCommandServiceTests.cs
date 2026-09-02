using MyAccountingApp.Application.DTOs;
using MyAccountingApp.Application.Services;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Interfaces;
using MyAccountingApp.Domain.ValueObjects;

namespace MyAccountingApp.Application.Tests.Services;

public class AssetTransactionCommandServiceTests
{
    [Fact]
    public void PatchMany_ShouldUpdateSymbol_ForAllMatchingIds()
    {
        FakePfRepo repo = new();
        AssetTransaction first = CreateAsset("AAPL");
        AssetTransaction second = CreateAsset("AAPL");
        repo.AddOrUpdate(first);
        repo.AddOrUpdate(second);
        AssetTransactionCommandService service = new(repo);

        BatchPatchResult result = service.PatchMany(
            new[] { first.Transaction.Id, second.Transaction.Id },
            new AssetTransactionPatch("TSLA"));

        Assert.Equal(2, result.Requested);
        Assert.Equal(2, result.Updated);
        Assert.Empty(result.Failures);
        Assert.Equal("TSLA", repo.GetAllTransactions().First(t => t.Transaction.Id == first.Transaction.Id).Symbol);
        Assert.Equal("TSLA", repo.GetAllTransactions().First(t => t.Transaction.Id == second.Transaction.Id).Symbol);
    }

    [Fact]
    public void PatchMany_ShouldReportMissingIds_AsFailures()
    {
        FakePfRepo repo = new();
        AssetTransaction existing = CreateAsset("AAPL");
        repo.AddOrUpdate(existing);
        AssetTransactionCommandService service = new(repo);
        Guid missing = Guid.NewGuid();

        BatchPatchResult result = service.PatchMany(
            new[] { existing.Transaction.Id, missing },
            new AssetTransactionPatch("TSLA"));

        Assert.Equal(2, result.Requested);
        Assert.Equal(1, result.Updated);
        BatchPatchFailure failure = Assert.Single(result.Failures);
        Assert.Equal(missing, failure.Id);
        Assert.Equal("Asset transaction not found.", failure.Error);
    }

    [Fact]
    public void PatchMany_ShouldReportInvalidSymbol_AsFailure_ForEveryId()
    {
        FakePfRepo repo = new();
        AssetTransaction first = CreateAsset("AAPL");
        AssetTransaction second = CreateAsset("AAPL");
        repo.AddOrUpdate(first);
        repo.AddOrUpdate(second);
        AssetTransactionCommandService service = new(repo);

        BatchPatchResult result = service.PatchMany(
            new[] { first.Transaction.Id, second.Transaction.Id },
            new AssetTransactionPatch(" "));

        Assert.Equal(0, result.Updated);
        Assert.Equal(2, result.Failures.Count);
        Assert.All(result.Failures, failure => Assert.Contains("cannot be null or empty", failure.Error));
        Assert.Equal("AAPL", repo.GetAllTransactions().First(t => t.Transaction.Id == first.Transaction.Id).Symbol);
        Assert.Equal("AAPL", repo.GetAllTransactions().First(t => t.Transaction.Id == second.Transaction.Id).Symbol);
    }

    [Fact]
    public void PatchMany_ShouldNotCountUnchangedSymbol_AsUpdated()
    {
        FakePfRepo repo = new();
        AssetTransaction existing = CreateAsset("AAPL");
        repo.AddOrUpdate(existing);
        AssetTransactionCommandService service = new(repo);

        BatchPatchResult result = service.PatchMany(
            new[] { existing.Transaction.Id },
            new AssetTransactionPatch("AAPL"));

        Assert.Equal(1, result.Requested);
        Assert.Equal(0, result.Updated);
        Assert.Empty(result.Failures);
        Assert.Equal("AAPL", repo.GetAllTransactions().Single().Symbol);
    }

    [Fact]
    public void PatchMany_ShouldCountDuplicateIdsOnce()
    {
        FakePfRepo repo = new();
        AssetTransaction existing = CreateAsset("AAPL");
        repo.AddOrUpdate(existing);
        AssetTransactionCommandService service = new(repo);

        BatchPatchResult result = service.PatchMany(
            new[] { existing.Transaction.Id, existing.Transaction.Id },
            new AssetTransactionPatch("TSLA"));

        Assert.Equal(1, result.Requested);
        Assert.Equal(1, result.Updated);
        Assert.Empty(result.Failures);
        Assert.Equal("TSLA", repo.GetAllTransactions().Single().Symbol);
    }

    [Fact]
    public void PatchMany_ShouldReportAllMissingIds_WhenNoneExist()
    {
        FakePfRepo repo = new();
        AssetTransactionCommandService service = new(repo);
        Guid missing = Guid.NewGuid();

        BatchPatchResult result = service.PatchMany(new[] { missing }, new AssetTransactionPatch("TSLA"));

        Assert.Equal(0, result.Updated);
        Assert.Single(result.Failures);
        Assert.Empty(repo.GetAllTransactions());
    }

    [Fact]
    public void DeleteMany_ShouldRemoveMatchingAssetTransactions()
    {
        FakePfRepo repo = new();
        AssetTransaction first = CreateAsset("AAPL");
        AssetTransaction second = CreateAsset("AAPL");
        repo.AddOrUpdate(first);
        repo.AddOrUpdate(second);
        AssetTransactionCommandService service = new(repo);

        BatchDeleteResult result = service.DeleteMany(new[] { first.Transaction.Id, second.Transaction.Id });

        Assert.Equal(2, result.Requested);
        Assert.Equal(2, result.Deleted);
        Assert.Empty(result.Failures);
        Assert.Empty(repo.GetAllTransactions());
    }

    [Fact]
    public void DeleteMany_ShouldReportMissingIds_AsFailures()
    {
        FakePfRepo repo = new();
        AssetTransaction existing = CreateAsset("AAPL");
        repo.AddOrUpdate(existing);
        AssetTransactionCommandService service = new(repo);
        Guid missing = Guid.NewGuid();

        BatchDeleteResult result = service.DeleteMany(new[] { existing.Transaction.Id, missing });

        Assert.Equal(2, result.Requested);
        Assert.Equal(1, result.Deleted);
        BatchPatchFailure failure = Assert.Single(result.Failures);
        Assert.Equal(missing, failure.Id);
        Assert.Equal("Asset transaction not found.", failure.Error);
        Assert.Empty(repo.GetAllTransactions());
    }

    private static AssetTransaction CreateAsset(string symbol)
    {
        Transaction transaction = new(
            Guid.NewGuid(),
            new DateTime(2026, 8, 1),
            "Test asset",
            new Money(100, "EUR"),
            TransactionCategory.EXPENSE);
        return new AssetTransaction(transaction, symbol, 2, AssetTransactionType.Buy);
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

        public bool Delete(Guid transactionId) => true;
        public int DeleteByYear(int year) => this._transactions.RemoveAll(t => t.Transaction.Date.Year == year);
    }
}
