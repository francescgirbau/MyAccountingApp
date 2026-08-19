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
        Assert.Equal(500m, result.NetCashFlow);
        Assert.Equal(2, result.TransactionCount);
        Assert.Equal(2, result.AssetTransactionCount);
        Assert.Equal(0, result.Transfers);
        Assert.Equal(0, result.Deposits);
        Assert.False(result.IncludesAssetCashFlows);
        Assert.NotEmpty(result.Months);
        MonthlySummaryDto month = Assert.Single(result.Months);
        Assert.Equal(6, month.Month);
        Assert.Equal(500m, month.Expenses);
        Assert.Equal(1000m, month.Income);
        Assert.Equal(300m, month.InvestmentPurchases);
        Assert.Equal(150m, month.InvestmentSales);
        Assert.Equal(500m, month.NetCashFlow);
        Assert.Equal(0, month.Transfers);
        Assert.Equal(0, month.Deposits);
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
    public void GetByYear_ReturnsMultipleMonths()
    {
        Transaction[] txs = new Transaction[]
        {
            Tx(2024, 3000, TransactionCategory.INCOME, 1),
            Tx(2024, 1000, TransactionCategory.EXPENSE, 1),
            Tx(2024, 2000, TransactionCategory.INCOME, 3),
            Tx(2024, 500, TransactionCategory.EXPENSE, 3),
        };
        var svc = CreateService(txs, Array.Empty<AssetTransaction>());

        AnnualSummaryDto? result = svc.GetByYear(2024);

        Assert.NotNull(result);
        Assert.Equal(2, result.Months.Count);
        Assert.Equal(1, result.Months[0].Month);
        Assert.Equal(2000m, result.Months[0].NetCashFlow);
        Assert.Equal(3, result.Months[1].Month);
        Assert.Equal(1500m, result.Months[1].NetCashFlow);
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
        Assert.Equal(3000m, result.NetCashFlow);
        Assert.Equal(1000m, result.InvestmentPurchases);
        Assert.Equal(800m, result.InvestmentSales);
    }

    [Fact]
    public void NetCashFlow_DoesNotDoubleCountInvestmentOutflows()
    {
        Transaction[] txs = new Transaction[]
        {
            Tx(2024, 3000, TransactionCategory.INCOME),
            Tx(2024, 1000, TransactionCategory.EXPENSE),
        };
        AssetTransaction[] assets = new AssetTransaction[]
        {
            AssetTx(2024, 1000, AssetTransactionType.Buy),
        };
        var svc = CreateService(txs, assets);

        AnnualSummaryDto? result = svc.GetByYear(2024);

        Assert.NotNull(result);
        Assert.Equal(2000m, result.NetCashFlow);
        Assert.Equal(1000m, result.InvestmentPurchases);
        Assert.Equal(1000m, result.Expenses);
    }

    [Fact]
    public void GetByYear_IncludesTransfersAndDeposits()
    {
        Transaction[] txs = new Transaction[]
        {
            Tx(2024, 3000, TransactionCategory.INCOME),
            Tx(2024, 1000, TransactionCategory.EXPENSE),
            Tx(2024, 500, TransactionCategory.TRANSFER),
            Tx(2024, 200, TransactionCategory.DEPOSIT),
        };
        var svc = CreateService(txs, Array.Empty<AssetTransaction>());

        AnnualSummaryDto? result = svc.GetByYear(2024);

        Assert.NotNull(result);
        Assert.Equal(3000m, result.Income);
        Assert.Equal(1000m, result.Expenses);
        Assert.Equal(500m, result.Transfers);
        Assert.Equal(200m, result.Deposits);
        Assert.Equal(2000m, result.NetCashFlow);

        MonthlySummaryDto month = Assert.Single(result.Months);
        Assert.Equal(500m, month.Transfers);
        Assert.Equal(200m, month.Deposits);
    }

    [Fact]
    public void GetByYear_ExcludesFxLegsFromTransfersAndDeposits()
    {
        Guid pairId = Guid.NewGuid();
        Transaction[] txs = new Transaction[]
        {
            Tx(2024, 200, TransactionCategory.TRANSFER),
            Tx(2024, 200, TransactionCategory.DEPOSIT),
            FxTx(2024, 490.24m, "EUR", FxLeg.Out, pairId),
            FxTx(2024, 545.20m, "USD", FxLeg.In, pairId),
        };
        var svc = CreateService(txs, Array.Empty<AssetTransaction>());

        AnnualSummaryDto? result = svc.GetByYear(2024);

        Assert.NotNull(result);
        Assert.Equal(200m, result.Transfers);
        Assert.Equal(200m, result.Deposits);
        Assert.Equal(490.24m, result.FxOut);
        Assert.Equal(0m, result.FxIn);
        Assert.Equal(-490.24m, result.FxNet);
        Assert.Equal(1, result.FxPairCount);
        Assert.Equal(0, result.FxUnmatchedLegCount);

        MonthlySummaryDto month = Assert.Single(result.Months);
        Assert.Equal(200m, month.Transfers);
        Assert.Equal(200m, month.Deposits);
        Assert.Equal(490.24m, month.FxOut);
        Assert.Equal(0m, month.FxIn);
        Assert.Equal(-490.24m, month.FxNet);
    }

    [Fact]
    public void GetByYear_FxInEurIsNotDeposit()
    {
        Guid pairId = Guid.NewGuid();
        Transaction[] txs = new Transaction[]
        {
            Tx(2024, 100, TransactionCategory.DEPOSIT),
            FxTx(2024, 2.40m, "EUR", FxLeg.In, pairId),
            FxTx(2024, 2.63m, "USD", FxLeg.Out, pairId),
        };
        var svc = CreateService(txs, Array.Empty<AssetTransaction>());

        AnnualSummaryDto? result = svc.GetByYear(2024);

        Assert.NotNull(result);
        Assert.Equal(100m, result.Deposits);
        Assert.Equal(0m, result.Transfers);
        Assert.Equal(2.40m, result.FxIn);
        Assert.Equal(0m, result.FxOut);
        Assert.Equal(2.40m, result.FxNet);
        Assert.Equal(1, result.FxPairCount);
    }

    [Fact]
    public void GetByYear_CountsUnmatchedFxLegs()
    {
        Transaction[] txs = new Transaction[]
        {
            FxTx(2024, 100m, "EUR", FxLeg.Out, Guid.NewGuid()),
        };
        var svc = CreateService(txs, Array.Empty<AssetTransaction>());

        AnnualSummaryDto? result = svc.GetByYear(2024);

        Assert.NotNull(result);
        Assert.Equal(1, result.FxPairCount);
        Assert.Equal(1, result.FxUnmatchedLegCount);
    }

    [Fact]
    public void GetByYear_FxDoesNotAffectIncomeOrExpense()
    {
        Guid pairId = Guid.NewGuid();
        Transaction[] txs = new Transaction[]
        {
            Tx(2024, 3000, TransactionCategory.INCOME),
            Tx(2024, 1000, TransactionCategory.EXPENSE),
            FxTx(2024, 500m, "EUR", FxLeg.Out, pairId),
            FxTx(2024, 545.20m, "USD", FxLeg.In, pairId),
        };
        var svc = CreateService(txs, Array.Empty<AssetTransaction>());

        AnnualSummaryDto? result = svc.GetByYear(2024);

        Assert.NotNull(result);
        Assert.Equal(3000m, result.Income);
        Assert.Equal(1000m, result.Expenses);
        Assert.Equal(2000m, result.NetCashFlow);
    }

    private static Transaction Tx(int year, decimal amount, TransactionCategory category = TransactionCategory.INCOME, int month = 6)
    {
        return new Transaction(
            Guid.NewGuid(),
            new DateTime(year, month, 1),
            "Test",
            new Money(amount, "EUR"),
            category);
    }

    private static Transaction FxTx(int year, decimal amount, string currency, FxLeg leg, Guid pairId, int month = 6)
    {
        return new Transaction(
            Guid.NewGuid(),
            new DateTime(year, month, 1),
            "FX EUR->USD",
            new Money(amount, currency),
            TransactionCategory.FX_CONVERSION,
            fxPairId: pairId,
            fxLeg: leg);
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

        public int DeleteByYear(int year) => this._transactions.RemoveAll(t => t.Date.Year == year);

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
        public int DeleteByYear(int year) => this._transactions.RemoveAll(t => t.Transaction.Date.Year == year);
    }
}
