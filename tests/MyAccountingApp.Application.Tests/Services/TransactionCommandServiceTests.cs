using MyAccountingApp.Application.DTOs;
using MyAccountingApp.Application.Services;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Interfaces;
using MyAccountingApp.Domain.ValueObjects;

namespace MyAccountingApp.Application.Tests.Services;

public class TransactionCommandServiceTests
{
    [Fact]
    public void PatchMany_ShouldUpdateCategory_ForAllMatchingIds()
    {
        FakeTxRepo repo = new();
        Transaction first = CreateTransaction();
        Transaction second = CreateTransaction();
        repo.Add(first);
        repo.Add(second);
        TransactionCommandService service = new(repo);

        BatchPatchResult result = service.PatchMany(
            new[] { first.Id, second.Id },
            new TransactionPatch("TRANSFER"));

        Assert.Equal(2, result.Requested);
        Assert.Equal(2, result.Updated);
        Assert.Empty(result.Failures);
        Assert.Equal(TransactionCategory.TRANSFER, repo.GetAll().First(t => t.Id == first.Id).Category);
        Assert.Equal(TransactionCategory.TRANSFER, repo.GetAll().First(t => t.Id == second.Id).Category);
    }

    [Fact]
    public void PatchMany_ShouldAcceptLowercaseCategory()
    {
        FakeTxRepo repo = new();
        Transaction existing = CreateTransaction();
        repo.Add(existing);
        TransactionCommandService service = new(repo);

        BatchPatchResult result = service.PatchMany(
            new[] { existing.Id },
            new TransactionPatch("income"));

        Assert.Equal(1, result.Updated);
        Assert.Equal(TransactionCategory.INCOME, repo.GetAll().Single().Category);
    }

    [Fact]
    public void PatchMany_ShouldReportMissingIds_AsFailures()
    {
        FakeTxRepo repo = new();
        Transaction existing = CreateTransaction();
        repo.Add(existing);
        TransactionCommandService service = new(repo);
        Guid missing = Guid.NewGuid();

        BatchPatchResult result = service.PatchMany(
            new[] { existing.Id, missing },
            new TransactionPatch("TRANSFER"));

        Assert.Equal(2, result.Requested);
        Assert.Equal(1, result.Updated);
        BatchPatchFailure failure = Assert.Single(result.Failures);
        Assert.Equal(missing, failure.Id);
        Assert.Equal("Transaction not found.", failure.Error);
    }

    [Fact]
    public void PatchMany_ShouldReportInvalidCategory_AsFailure()
    {
        FakeTxRepo repo = new();
        Transaction existing = CreateTransaction();
        repo.Add(existing);
        TransactionCommandService service = new(repo);

        BatchPatchResult result = service.PatchMany(
            new[] { existing.Id },
            new TransactionPatch("NOT_A_CATEGORY"));

        Assert.Equal(0, result.Updated);
        BatchPatchFailure failure = Assert.Single(result.Failures);
        Assert.Equal(existing.Id, failure.Id);
        Assert.Contains("Invalid category", failure.Error);
        Assert.Equal(TransactionCategory.EXPENSE, repo.GetAll().Single().Category);
    }

    [Fact]
    public void PatchMany_ShouldNotCountUnchangedCategory_AsUpdated()
    {
        FakeTxRepo repo = new();
        Transaction existing = CreateTransaction();
        repo.Add(existing);
        TransactionCommandService service = new(repo);

        BatchPatchResult result = service.PatchMany(
            new[] { existing.Id },
            new TransactionPatch("EXPENSE"));

        Assert.Equal(1, result.Requested);
        Assert.Equal(0, result.Updated);
        Assert.Empty(result.Failures);
        Assert.Equal(TransactionCategory.EXPENSE, repo.GetAll().Single().Category);
    }

    [Fact]
    public void PatchMany_ShouldCountDuplicateIdsOnce()
    {
        FakeTxRepo repo = new();
        Transaction existing = CreateTransaction();
        repo.Add(existing);
        TransactionCommandService service = new(repo);

        BatchPatchResult result = service.PatchMany(
            new[] { existing.Id, existing.Id },
            new TransactionPatch("TRANSFER"));

        Assert.Equal(1, result.Requested);
        Assert.Equal(1, result.Updated);
        Assert.Empty(result.Failures);
        Assert.Equal(TransactionCategory.TRANSFER, repo.GetAll().Single().Category);
    }

    [Fact]
    public void DeleteMany_ShouldRemoveMatchingTransactions()
    {
        FakeTxRepo repo = new();
        Transaction first = CreateTransaction();
        Transaction second = CreateTransaction();
        repo.Add(first);
        repo.Add(second);
        TransactionCommandService service = new(repo);

        BatchDeleteResult result = service.DeleteMany(new[] { first.Id, second.Id });

        Assert.Equal(2, result.Requested);
        Assert.Equal(2, result.Deleted);
        Assert.Empty(result.Failures);
        Assert.Empty(repo.GetAll());
    }

    [Fact]
    public void DeleteMany_ShouldReportMissingIds_AsFailures()
    {
        FakeTxRepo repo = new();
        Transaction existing = CreateTransaction();
        repo.Add(existing);
        TransactionCommandService service = new(repo);
        Guid missing = Guid.NewGuid();

        BatchDeleteResult result = service.DeleteMany(new[] { existing.Id, missing });

        Assert.Equal(2, result.Requested);
        Assert.Equal(1, result.Deleted);
        BatchPatchFailure failure = Assert.Single(result.Failures);
        Assert.Equal(missing, failure.Id);
        Assert.Equal("Transaction not found.", failure.Error);
        Assert.Empty(repo.GetAll());
    }

    [Fact]
    public void DeleteMany_ShouldCountDuplicateIdsOnce()
    {
        FakeTxRepo repo = new();
        Transaction existing = CreateTransaction();
        repo.Add(existing);
        TransactionCommandService service = new(repo);

        BatchDeleteResult result = service.DeleteMany(new[] { existing.Id, existing.Id });

        Assert.Equal(1, result.Requested);
        Assert.Equal(1, result.Deleted);
        Assert.Empty(result.Failures);
        Assert.Empty(repo.GetAll());
    }

    private static Transaction CreateTransaction()
    {
        return new Transaction(
            Guid.NewGuid(),
            new DateTime(2026, 8, 1),
            "Test transaction",
            new Money(100, "EUR"),
            TransactionCategory.EXPENSE);
    }

    private sealed class FakeTxRepo : ITransactionRepository
    {
        private readonly List<Transaction> _transactions = new();

        public void Add(Transaction transaction) => this._transactions.Add(transaction);
        public void AddOrUpdate(Transaction transaction) => this._transactions.Add(transaction);
        public IEnumerable<Transaction> GetAll() => this._transactions;
        public void Initialize(IEnumerable<Transaction> transactions)
        {
            this._transactions.Clear();
            this._transactions.AddRange(transactions);
        }

        public bool Delete(Transaction transaction) => this._transactions.Remove(transaction);
        public int DeleteByYear(int year) => this._transactions.RemoveAll(t => t.Date.Year == year);
    }
}