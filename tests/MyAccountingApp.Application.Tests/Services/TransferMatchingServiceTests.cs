using System;
using System.Collections.Generic;
using System.Linq;
using MyAccountingApp.Application.Interfaces;
using MyAccountingApp.Application.Services;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Interfaces;
using MyAccountingApp.Domain.ValueObjects;
using Xunit;

namespace MyAccountingApp.Application.Tests.Services;

public class TransferMatchingServiceTests
{
    [Fact]
    public void Recalculate_NoTransactions_ReturnsZeros()
    {
        CountingTxRepo repo = new CountingTxRepo(Array.Empty<Transaction>());
        TransferMatchingService service = new TransferMatchingService(repo);

        TransferMatchingResult result = service.Recalculate();

        Assert.Equal(0, result.TransferCount);
        Assert.Equal(0, result.MatchedPairs);
        Assert.Equal(0, result.UnmatchedTransfers);
        Assert.Equal(0, result.ChangedTransactions);
        Assert.Equal(0, repo.InitializeCount);
    }

    [Fact]
    public void Recalculate_PairsTransferWithDeposit_WithoutMutating()
    {
        Guid transferId = Guid.NewGuid();
        CountingTxRepo repo = new CountingTxRepo(new[]
        {
            new Transaction(transferId, new DateTime(2019, 5, 20), "TARGETA *9027", new Money(200, "EUR"), TransactionCategory.TRANSFER),
            new Transaction(Guid.NewGuid(), new DateTime(2019, 5, 20), "Top-up by *9027", new Money(200, "EUR"), TransactionCategory.DEPOSIT),
        });
        TransferMatchingService service = new TransferMatchingService(repo);

        TransferMatchingResult result = service.Recalculate();

        Assert.Equal(1, result.TransferCount);
        Assert.Equal(1, result.MatchedPairs);
        Assert.Equal(0, result.UnmatchedTransfers);
        Assert.Equal(0, result.ChangedTransactions);
        Assert.Equal(0, repo.InitializeCount);

        List<Transaction> all = repo.GetAll().ToList();
        Assert.Equal(TransactionCategory.TRANSFER, all.Single(t => t.Id == transferId).Category);
    }

    [Fact]
    public void Recalculate_UnmatchedTransfer_CountsIt()
    {
        CountingTxRepo repo = new CountingTxRepo(new[]
        {
            new Transaction(Guid.NewGuid(), new DateTime(2019, 8, 12), "TARGETA *9027", new Money(600, "EUR"), TransactionCategory.TRANSFER),
            new Transaction(Guid.NewGuid(), new DateTime(2019, 8, 12), "TARGETA *9027 2", new Money(200, "EUR"), TransactionCategory.TRANSFER),
            new Transaction(Guid.NewGuid(), new DateTime(2019, 8, 12), "Top-up by *9027", new Money(600, "EUR"), TransactionCategory.DEPOSIT),
        });
        TransferMatchingService service = new TransferMatchingService(repo);

        TransferMatchingResult result = service.Recalculate();

        Assert.Equal(2, result.TransferCount);
        Assert.Equal(1, result.MatchedPairs);
        Assert.Equal(1, result.UnmatchedTransfers);
        Assert.Equal(0, result.ChangedTransactions);
    }

    [Fact]
    public void Recalculate_Twice_IsIdempotent()
    {
        CountingTxRepo repo = new CountingTxRepo(new[]
        {
            new Transaction(Guid.NewGuid(), new DateTime(2019, 8, 12), "T", new Money(200, "EUR"), TransactionCategory.TRANSFER),
            new Transaction(Guid.NewGuid(), new DateTime(2019, 8, 12), "D", new Money(200, "EUR"), TransactionCategory.DEPOSIT),
        });
        TransferMatchingService service = new TransferMatchingService(repo);

        TransferMatchingResult first = service.Recalculate();
        TransferMatchingResult second = service.Recalculate();

        Assert.Equal(1, first.MatchedPairs);
        Assert.Equal(1, second.MatchedPairs);
        Assert.Equal(0, second.ChangedTransactions);
        Assert.Equal(0, repo.InitializeCount);
    }

    [Fact]
    public void Recalculate_DoesNotPairTransfersAmongThemselves()
    {
        CountingTxRepo repo = new CountingTxRepo(new[]
        {
            new Transaction(Guid.NewGuid(), new DateTime(2019, 8, 12), "T 1", new Money(200, "EUR"), TransactionCategory.TRANSFER),
            new Transaction(Guid.NewGuid(), new DateTime(2019, 8, 12), "T 2", new Money(200, "EUR"), TransactionCategory.TRANSFER),
        });
        TransferMatchingService service = new TransferMatchingService(repo);

        TransferMatchingResult result = service.Recalculate();

        Assert.Equal(0, result.MatchedPairs);
        Assert.Equal(2, result.UnmatchedTransfers);
    }

    private sealed class CountingTxRepo : ITransactionRepository
    {
        private readonly List<Transaction> _transactions;

        public CountingTxRepo(IEnumerable<Transaction> transactions) => this._transactions = transactions.ToList();

        public int InitializeCount { get; private set; }

        public void AddOrUpdate(Transaction transaction) => this._transactions.Add(transaction);

        public bool Delete(Transaction transaction) => this._transactions.Remove(transaction);

        public IEnumerable<Transaction> GetAll() => this._transactions;

        public int DeleteByYear(int year) => this._transactions.RemoveAll(t => t.Date.Year == year);

        public void Initialize(IEnumerable<Transaction> transactions)
        {
            this.InitializeCount++;
            this._transactions.Clear();
            this._transactions.AddRange(transactions);
        }
    }
}