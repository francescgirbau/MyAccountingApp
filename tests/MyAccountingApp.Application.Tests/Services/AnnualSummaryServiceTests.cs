namespace MyAccountingApp.Application.Tests.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using MyAccountingApp.Application.DTOs;
using MyAccountingApp.Application.Interfaces;
using MyAccountingApp.Application.Services;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Interfaces;
using MyAccountingApp.Domain.ValueObjects;
using Xunit;

public class AnnualSummaryServiceTests
{
    [Fact]
    public void GetAll_ReturnsAllYears()
    {
        var svc = CreateService(
            new Transaction[]
            {
                Tx(2020, 100, TransactionCategory.INCOME),
                Tx(2020, 50, TransactionCategory.EXPENSE),
                Tx(2021, 200, TransactionCategory.INCOME),
            },
            Array.Empty<AssetTransaction>());

        List<AnnualSummaryDto> result = svc.GetAll();

        Assert.Equal(2, result.Count);
        Assert.Equal(2020, result[0].Year);
        Assert.Equal(2021, result[1].Year);
    }

    [Fact]
    public void GetByYear_ReturnsCorrectSummary()
    {
        Transaction[] txs = new Transaction[]
        {
            Tx(2020, 1000, TransactionCategory.INCOME),
            Tx(2020, 500, TransactionCategory.EXPENSE),
            Tx(2021, 200, TransactionCategory.INCOME),
        };
        AssetTransaction[] assets = new AssetTransaction[]
        {
            AssetTx(2020, 300, AssetTransactionType.Buy),
            AssetTx(2020, 150, AssetTransactionType.Sell),
        };
        var svc = CreateService(txs, assets);

        AnnualSummaryDto? result = svc.GetByYear(2020);

        Assert.NotNull(result);
        Assert.Equal(2020, result.Year);
        Assert.Equal(500m, result.Expenses);
        Assert.Equal(1000m, result.Income);
        Assert.Equal(300m, result.InvestmentPurchases);
        Assert.Equal(150m, result.InvestmentSales);
        Assert.Equal(350m, result.NetCashFlow);
        Assert.Equal(2, result.TransactionCount);
        Assert.Equal(2, result.AssetTransactionCount);
    }

    [Fact]
    public void GetByYear_ReturnsNullForEmptyYear()
    {
        var svc = CreateService(Array.Empty<Transaction>(), Array.Empty<AssetTransaction>());

        AnnualSummaryDto? result = svc.GetByYear(1999);

        Assert.Null(result);
    }

    [Fact]
    public void GetAll_HandlesNoData()
    {
        var svc = CreateService(Array.Empty<Transaction>(), Array.Empty<AssetTransaction>());

        List<AnnualSummaryDto> result = svc.GetAll();

        Assert.Empty(result);
    }

    [Fact]
    public void NetCashFlow_IsCorrect()
    {
        Transaction[] txs = new Transaction[]
        {
            Tx(2024, 5000, TransactionCategory.INCOME),
            Tx(2024, 2000, TransactionCategory.EXPENSE),
        };
        AssetTransaction[] assets = new AssetTransaction[]
        {
            AssetTx(2024, 1000, AssetTransactionType.Buy),
            AssetTx(2024, 800, AssetTransactionType.Sell),
        };
        var svc = CreateService(txs, assets);

        AnnualSummaryDto? result = svc.GetByYear(2024);

        Assert.NotNull(result);
        Assert.Equal(2800m, result.NetCashFlow);
    }

    private static Transaction Tx(int year, decimal amount, TransactionCategory category = TransactionCategory.INCOME)
    {
        return new Transaction(
            Guid.NewGuid(),
            new DateTime(year, 6, 1),
            "Test",
            new Money(amount, "EUR"),
            category);
    }

    private static AssetTransaction AssetTx(int year, decimal amount, AssetTransactionType type)
    {
        TransactionCategory cat = type == AssetTransactionType.Buy
            ? TransactionCategory.EXPENSE
            : TransactionCategory.INCOME;

        Transaction tx = new Transaction(
            Guid.NewGuid(),
            new DateTime(year, 6, 1),
            "Asset",
            new Money(amount, "EUR"),
            cat);

        return new AssetTransaction(tx, "TEST", 10, type);
    }

    private static IAnnualSummaryService CreateService(
        IEnumerable<Transaction> transactions,
        IEnumerable<AssetTransaction> assetTransactions)
    {
        return new AnnualSummaryService(
            new FakeTxRepo(transactions),
            new FakePfRepo(assetTransactions));
    }

    private sealed class FakeTxRepo : ITransactionRepository
    {
        private readonly List<Transaction> _transactions;

        public FakeTxRepo(IEnumerable<Transaction> txs) => this._transactions = txs.ToList();

        public void AddOrUpdate(Transaction tx) => this._transactions.Add(tx);

        public bool Delete(Transaction transaction) => this._transactions.Remove(transaction);

        public IEnumerable<Transaction> GetAll() => this._transactions;

        public void Initialize(IEnumerable<Transaction> transactions)
        {
            this._transactions.Clear();
            this._transactions.AddRange(transactions);
        }
    }

    private sealed class FakePfRepo : IPortfolioRepository
    {
        private readonly List<AssetTransaction> _transactions;

        public FakePfRepo(IEnumerable<AssetTransaction> txs) => this._transactions = txs.ToList();

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
    }
}
