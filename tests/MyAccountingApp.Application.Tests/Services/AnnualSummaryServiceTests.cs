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
        Assert.Equal(500m, result.Operating.Expenses);
        Assert.Equal(1000m, result.Operating.Income);
        Assert.Equal(300m, result.Investing.Purchases);
        Assert.Equal(150m, result.Investing.Sales);
        Assert.Equal(500m, result.Operating.NetOperatingCashFlow);
        Assert.Equal(2, result.TransactionCount);
        Assert.Equal(2, result.AssetTransactionCount);
        Assert.Equal(0m, result.Internal.Transfers);
        Assert.Equal(0m, result.Internal.Deposits);
        Assert.False(result.IncludesAssetCashFlows);
        Assert.NotEmpty(result.Months);
        MonthlySummaryDto month = Assert.Single(result.Months);
        Assert.Equal(6, month.Month);
        Assert.Equal(500m, month.Operating.Expenses);
        Assert.Equal(1000m, month.Operating.Income);
        Assert.Equal(300m, month.Investing.Purchases);
        Assert.Equal(150m, month.Investing.Sales);
        Assert.Equal(500m, month.Operating.NetOperatingCashFlow);
        Assert.Equal(0m, month.Internal.Transfers);
        Assert.Equal(0m, month.Internal.Deposits);
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
        Assert.Equal(2000m, result.Months[0].Operating.NetOperatingCashFlow);
        Assert.Equal(3, result.Months[1].Month);
        Assert.Equal(1500m, result.Months[1].Operating.NetOperatingCashFlow);
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
        Assert.Equal(3000m, result.Operating.NetOperatingCashFlow);
        Assert.Equal(1000m, result.Investing.Purchases);
        Assert.Equal(800m, result.Investing.Sales);
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
        Assert.Equal(2000m, result.Operating.NetOperatingCashFlow);
        Assert.Equal(1000m, result.Investing.Purchases);
        Assert.Equal(1000m, result.Operating.Expenses);
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
        Assert.Equal(3000m, result.Operating.Income);
        Assert.Equal(1000m, result.Operating.Expenses);
        Assert.Equal(500m, result.Internal.Transfers);
        Assert.Equal(200m, result.Internal.Deposits);
        Assert.Equal(2000m, result.Operating.NetOperatingCashFlow);

        MonthlySummaryDto month = Assert.Single(result.Months);
        Assert.Equal(500m, month.Internal.Transfers);
        Assert.Equal(200m, month.Internal.Deposits);
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
        Assert.Equal(200m, result.Internal.Transfers);
        Assert.Equal(200m, result.Internal.Deposits);
        Assert.Equal(490.24m, result.Internal.FxOut);
        Assert.Equal(0m, result.Internal.FxIn);
        Assert.Equal(-490.24m, result.Internal.FxNet);
        Assert.Equal(1, result.Internal.FxPairCount);
        Assert.Equal(0, result.Internal.FxUnmatchedLegCount);

        MonthlySummaryDto month = Assert.Single(result.Months);
        Assert.Equal(200m, month.Internal.Transfers);
        Assert.Equal(200m, month.Internal.Deposits);
        Assert.Equal(490.24m, month.Internal.FxOut);
        Assert.Equal(0m, month.Internal.FxIn);
        Assert.Equal(-490.24m, month.Internal.FxNet);
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
        Assert.Equal(100m, result.Internal.Deposits);
        Assert.Equal(0m, result.Internal.Transfers);
        Assert.Equal(2.40m, result.Internal.FxIn);
        Assert.Equal(0m, result.Internal.FxOut);
        Assert.Equal(2.40m, result.Internal.FxNet);
        Assert.Equal(1, result.Internal.FxPairCount);
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
        Assert.Equal(1, result.Internal.FxPairCount);
        Assert.Equal(1, result.Internal.FxUnmatchedLegCount);
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
        Assert.Equal(3000m, result.Operating.Income);
        Assert.Equal(1000m, result.Operating.Expenses);
        Assert.Equal(2000m, result.Operating.NetOperatingCashFlow);
    }

    [Fact]
    public void GetByYear_InvestmentCashFlows_DoNotAffectOperatingPnL()
    {
        Guid pairId = Guid.NewGuid();
        Transaction[] txs = new Transaction[]
        {
            Tx(2024, 3000, TransactionCategory.INCOME),        // salary
            Tx(2024, 1000, TransactionCategory.EXPENSE),       // supermarket
            FxTx(2024, 500m, "EUR", FxLeg.Out, pairId),        // MSFT buy (INVESTMENT)
            FxTx(2024, 545.20m, "USD", FxLeg.In, pairId),
        };
        AssetTransaction[] assets = new AssetTransaction[]
        {
            AssetTx(2024, 500, AssetTransactionType.Buy),      // MSFT buy
        };
        var svc = CreateService(txs, assets);

        AnnualSummaryDto? result = svc.GetByYear(2024);

        Assert.NotNull(result);
        Assert.Equal(3000m, result.Operating.Income);        // salary only
        Assert.Equal(1000m, result.Operating.Expenses);      // supermarket only
        Assert.Equal(2000m, result.Operating.NetOperatingCashFlow);   // income - expense only
        Assert.Equal(500m, result.Investing.Purchases); // from AssetTransaction
        Assert.Equal(0m, result.Investing.Sales);
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
            ? TransactionCategory.INVESTMENT
            : TransactionCategory.DIVESTMENT;

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