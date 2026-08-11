using MyAccountingApp.Application.DTOs;
using MyAccountingApp.Application.Services;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Interfaces;
using MyAccountingApp.Domain.ValueObjects;

namespace MyAccountingApp.Application.Tests.Services;

public class OptionTransactionCommandServiceTests
{
    [Fact]
    public void PatchMany_ShouldUpdateSymbol_ForAllMatchingIds()
    {
        FakeOptionRepo repo = new();
        OptionTransaction first = CreateOption("AAPL");
        OptionTransaction second = CreateOption("AAPL");
        repo.Add(first);
        repo.Add(second);
        OptionTransactionCommandService service = new(repo);

        BatchPatchResult result = service.PatchMany(
            new[] { first.Transaction.Id, second.Transaction.Id },
            new OptionTransactionPatch("TSLA"));

        Assert.Equal(2, result.Requested);
        Assert.Equal(2, result.Updated);
        Assert.Empty(result.Failures);
        Assert.Equal("TSLA", repo.GetAll().First(t => t.Transaction.Id == first.Transaction.Id).Symbol);
        Assert.Equal("TSLA", repo.GetAll().First(t => t.Transaction.Id == second.Transaction.Id).Symbol);
    }

    [Fact]
    public void PatchMany_ShouldReportMissingIds_AsFailures()
    {
        FakeOptionRepo repo = new();
        OptionTransaction existing = CreateOption("AAPL");
        repo.Add(existing);
        OptionTransactionCommandService service = new(repo);
        Guid missing = Guid.NewGuid();

        BatchPatchResult result = service.PatchMany(
            new[] { existing.Transaction.Id, missing },
            new OptionTransactionPatch("TSLA"));

        Assert.Equal(2, result.Requested);
        Assert.Equal(1, result.Updated);
        BatchPatchFailure failure = Assert.Single(result.Failures);
        Assert.Equal(missing, failure.Id);
        Assert.Equal("Option transaction not found.", failure.Error);
    }

    [Fact]
    public void PatchMany_ShouldReportInvalidSymbol_AsFailure_ForEveryId()
    {
        FakeOptionRepo repo = new();
        OptionTransaction first = CreateOption("AAPL");
        OptionTransaction second = CreateOption("AAPL");
        repo.Add(first);
        repo.Add(second);
        OptionTransactionCommandService service = new(repo);

        BatchPatchResult result = service.PatchMany(
            new[] { first.Transaction.Id, second.Transaction.Id },
            new OptionTransactionPatch(" "));

        Assert.Equal(0, result.Updated);
        Assert.Equal(2, result.Failures.Count);
        Assert.All(result.Failures, failure => Assert.Contains("cannot be null or empty", failure.Error));
    }

    [Fact]
    public void PatchMany_ShouldNotCountUnchangedSymbol_AsUpdated()
    {
        FakeOptionRepo repo = new();
        OptionTransaction existing = CreateOption("AAPL");
        repo.Add(existing);
        OptionTransactionCommandService service = new(repo);

        BatchPatchResult result = service.PatchMany(
            new[] { existing.Transaction.Id },
            new OptionTransactionPatch("AAPL"));

        Assert.Equal(1, result.Requested);
        Assert.Equal(0, result.Updated);
        Assert.Empty(result.Failures);
        Assert.Equal("AAPL", repo.GetAll().Single().Symbol);
    }

    [Fact]
    public void PatchMany_ShouldCountDuplicateIdsOnce()
    {
        FakeOptionRepo repo = new();
        OptionTransaction existing = CreateOption("AAPL");
        repo.Add(existing);
        OptionTransactionCommandService service = new(repo);

        BatchPatchResult result = service.PatchMany(
            new[] { existing.Transaction.Id, existing.Transaction.Id },
            new OptionTransactionPatch("TSLA"));

        Assert.Equal(1, result.Requested);
        Assert.Equal(1, result.Updated);
        Assert.Empty(result.Failures);
        Assert.Equal("TSLA", repo.GetAll().Single().Symbol);
    }

    private static OptionTransaction CreateOption(string symbol)
    {
        Transaction transaction = new(
            Guid.NewGuid(),
            new DateTime(2026, 8, 1),
            "Test option",
            new Money(100, "EUR"),
            TransactionCategory.EXPENSE);
        return new OptionTransaction(transaction, symbol, "US0378331005", 2, AssetTransactionType.Buy);
    }

    private sealed class FakeOptionRepo : IOptionTransactionRepository
    {
        private readonly List<OptionTransaction> _transactions = new();

        public void Add(OptionTransaction transaction) => this._transactions.Add(transaction);
        public IEnumerable<OptionTransaction> GetAll() => this._transactions;
        public void Initialize(IEnumerable<OptionTransaction> transactions)
        {
            this._transactions.Clear();
            this._transactions.AddRange(transactions);
        }

        public void Update(OptionTransaction transaction)
        {
        }

        public bool Delete(Guid id) => true;
        public int DeleteByYear(int year) => this._transactions.RemoveAll(t => t.Transaction.Date.Year == year);
    }
}