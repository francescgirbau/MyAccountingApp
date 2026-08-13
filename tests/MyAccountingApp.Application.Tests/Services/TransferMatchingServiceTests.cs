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
    public void Recalculate_ValidPair_RecategorizesAndPersists()
    {
        Guid expenseId = Guid.NewGuid();
        Guid incomeId = Guid.NewGuid();
        CountingTxRepo repo = new CountingTxRepo(new[]
        {
            new Transaction(expenseId, new DateTime(2024, 3, 1), "Transferencia a FRANCESC GIRBAU IBKR", new Money(1000, "EUR"), TransactionCategory.EXPENSE),
            new Transaction(incomeId, new DateTime(2024, 3, 1), "Transferencia de FRANCESC a IBKR", new Money(1000, "EUR"), TransactionCategory.INCOME),
        });
        TransferMatchingService service = new TransferMatchingService(repo);

        TransferMatchingResult result = service.Recalculate();

        Assert.Equal(2, result.TransferCount);
        Assert.Equal(1, result.MatchedPairs);
        Assert.Equal(0, result.UnmatchedTransfers);
        Assert.Equal(2, result.ChangedTransactions);
        Assert.Equal(1, repo.InitializeCount);

        List<Transaction> all = repo.GetAll().ToList();
        Assert.Equal(TransactionCategory.TRANSFER, all.Single(t => t.Id == expenseId).Category);
        Assert.Equal(TransactionCategory.TRANSFER, all.Single(t => t.Id == incomeId).Category);
    }

    [Fact]
    public void Recalculate_SecondRun_IsIdempotent()
    {
        CountingTxRepo repo = new CountingTxRepo(new[]
        {
            new Transaction(Guid.NewGuid(), new DateTime(2024, 3, 1), "Transferencia a FRANCESC GIRBAU IBKR", new Money(1000, "EUR"), TransactionCategory.EXPENSE),
            new Transaction(Guid.NewGuid(), new DateTime(2024, 3, 1), "Transferencia de FRANCESC a IBKR", new Money(1000, "EUR"), TransactionCategory.INCOME),
        });
        TransferMatchingService service = new TransferMatchingService(repo);

        TransferMatchingResult first = service.Recalculate();
        TransferMatchingResult second = service.Recalculate();

        Assert.Equal(2, first.ChangedTransactions);
        Assert.Equal(0, second.ChangedTransactions);
        Assert.Equal(0, second.MatchedPairs);
        Assert.Equal(0, second.UnmatchedTransfers);
        Assert.Equal(2, first.TransferCount);
        Assert.Equal(1, repo.InitializeCount);
    }

    [Fact]
    public void Recalculate_OrphanTransfer_CountsAsUnmatched()
    {
        CountingTxRepo repo = new CountingTxRepo(new[]
        {
            new Transaction(Guid.NewGuid(), new DateTime(2024, 3, 1), "Transferencia a FRANCESC GIRBAU IBKR", new Money(1000, "EUR"), TransactionCategory.EXPENSE),
        });
        TransferMatchingService service = new TransferMatchingService(repo);

        TransferMatchingResult result = service.Recalculate();

        Assert.Equal(1, result.TransferCount);
        Assert.Equal(0, result.MatchedPairs);
        Assert.Equal(1, result.UnmatchedTransfers);
        Assert.Equal(0, result.ChangedTransactions);
        Assert.Equal(0, repo.InitializeCount);
        Assert.Equal(TransactionCategory.EXPENSE, repo.GetAll().Single().Category);
    }

    [Fact]
    public void Recalculate_NonTransferTransactions_Untouched()
    {
        Transaction expense = new Transaction(Guid.NewGuid(), new DateTime(2024, 3, 1), "Supermarket", new Money(50, "EUR"), TransactionCategory.EXPENSE);
        Transaction income = new Transaction(Guid.NewGuid(), new DateTime(2024, 3, 2), "Bonus", new Money(500, "EUR"), TransactionCategory.INCOME);
        CountingTxRepo repo = new CountingTxRepo(new[] { expense, income });
        TransferMatchingService service = new TransferMatchingService(repo);

        TransferMatchingResult result = service.Recalculate();

        Assert.Equal(0, result.TransferCount);
        Assert.Equal(0, result.MatchedPairs);
        Assert.Equal(0, result.UnmatchedTransfers);
        Assert.Equal(0, result.ChangedTransactions);
        Assert.Equal(0, repo.InitializeCount);
        Assert.Equal(TransactionCategory.EXPENSE, repo.GetAll().ToList()[0].Category);
        Assert.Equal(TransactionCategory.INCOME, repo.GetAll().ToList()[1].Category);
    }

    [Fact]
    public void Recalculate_KeepsFields_WhenRecategorizing()
    {
        Guid expenseId = Guid.NewGuid();
        DateTime date = new DateTime(2024, 3, 1);
        const string description = "Transferencia a FRANCESC GIRBAU IBKR FROM CAIXA";
        const string source = "tx_2024.csv";
        CountingTxRepo repo = new CountingTxRepo(new[]
        {
            new Transaction(expenseId, date, description, new Money(1000, "EUR"), TransactionCategory.EXPENSE, source),
            new Transaction(Guid.NewGuid(), date, "Transferencia de FRANCESC A IBKR", new Money(1000, "EUR"), TransactionCategory.INCOME),
        });
        TransferMatchingService service = new TransferMatchingService(repo);

        service.Recalculate();

        Transaction updated = repo.GetAll().Single(t => t.Id == expenseId);
        Assert.Equal(TransactionCategory.TRANSFER, updated.Category);
        Assert.Equal(date, updated.Date);
        Assert.Equal(description, updated.Description);
        Assert.Equal(new Money(1000, "EUR"), updated.Money);
        Assert.Equal(source, updated.Source);
    }

    [Fact]
    public void Recalculate_DoesNotMatch_WhenAmountDiffers()
    {
        CountingTxRepo repo = new CountingTxRepo(new[]
        {
            new Transaction(Guid.NewGuid(), new DateTime(2024, 3, 1), "Transferencia a FRANCESC GIRBAU IBKR", new Money(1000, "EUR"), TransactionCategory.EXPENSE),
            new Transaction(Guid.NewGuid(), new DateTime(2024, 3, 1), "Transferencia de FRANCESC a IBKR", new Money(500, "EUR"), TransactionCategory.INCOME),
        });
        TransferMatchingService service = new TransferMatchingService(repo);

        TransferMatchingResult result = service.Recalculate();

        Assert.Equal(0, result.MatchedPairs);
        Assert.Equal(2, result.UnmatchedTransfers);
        Assert.Equal(0, result.ChangedTransactions);
    }

    [Fact]
    public void Recalculate_DoesNotMatch_WhenCurrencyDiffers()
    {
        CountingTxRepo repo = new CountingTxRepo(new[]
        {
            new Transaction(Guid.NewGuid(), new DateTime(2024, 3, 1), "Transferencia a FRANCESC GIRBAU IBKR", new Money(1000, "EUR"), TransactionCategory.EXPENSE),
            new Transaction(Guid.NewGuid(), new DateTime(2024, 3, 1), "Transferencia de FRANCESC a IBKR", new Money(1000, "USD"), TransactionCategory.INCOME),
        });
        TransferMatchingService service = new TransferMatchingService(repo);

        TransferMatchingResult result = service.Recalculate();

        Assert.Equal(0, result.MatchedPairs);
        Assert.Equal(2, result.UnmatchedTransfers);
        Assert.Equal(0, result.ChangedTransactions);
    }

    [Fact]
    public void Recalculate_DoesNotMatch_WhenGapOverThreeDays()
    {
        CountingTxRepo repo = new CountingTxRepo(new[]
        {
            new Transaction(Guid.NewGuid(), new DateTime(2024, 3, 1), "Transferencia a FRANCESC GIRBAU IBKR", new Money(1000, "EUR"), TransactionCategory.EXPENSE),
            new Transaction(Guid.NewGuid(), new DateTime(2024, 3, 5), "Transferencia de FRANCESC a IBKR", new Money(1000, "EUR"), TransactionCategory.INCOME),
        });
        TransferMatchingService service = new TransferMatchingService(repo);

        TransferMatchingResult result = service.Recalculate();

        Assert.Equal(0, result.MatchedPairs);
        Assert.Equal(2, result.UnmatchedTransfers);
        Assert.Equal(0, result.ChangedTransactions);
    }

    [Fact]
    public void Recalculate_AmbiguousPair_FirstIncomeWinsAndIsReusable()
    {
        Guid expenseA = Guid.NewGuid();
        Guid expenseB = Guid.NewGuid();
        Guid income = Guid.NewGuid();
        CountingTxRepo repo = new CountingTxRepo(new[]
        {
            new Transaction(expenseA, new DateTime(2024, 3, 1), "Transferencia a FRANCESC GIRBAU IBKR", new Money(1000, "EUR"), TransactionCategory.EXPENSE),
            new Transaction(expenseB, new DateTime(2024, 3, 1), "Transferencia de FRANCESC a IBKR 2", new Money(1000, "EUR"), TransactionCategory.EXPENSE),
            new Transaction(income, new DateTime(2024, 3, 1), "Transferencia de FRANCESC a IBKR", new Money(1000, "EUR"), TransactionCategory.INCOME),
        });
        TransferMatchingService service = new TransferMatchingService(repo);

        TransferMatchingResult result = service.Recalculate();

        Assert.Equal(2, result.MatchedPairs);
        Assert.Equal(4, result.ChangedTransactions);
        Assert.Equal(0, result.UnmatchedTransfers);
        Assert.All(repo.GetAll(), t => Assert.Equal(TransactionCategory.TRANSFER, t.Category));
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