using Microsoft.Extensions.Logging;
using Moq;
using MyAccountingApp.Core.Agents;
using MyAccountingApp.Core.Models;
using MyAccountingApp.Core.Services;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;

namespace MyAccountingApp.Core.Tests.Agents;

public class InteractiveBrokersImportServiceExtraTests
{
    private readonly Mock<ICsvParser> parserMock = new();
    private readonly InteractiveBrokersImportService agent;

    public InteractiveBrokersImportServiceExtraTests()
    {
        Mock<ILogger<InteractiveBrokersImportService>> loggerMock = new();
        this.agent = new InteractiveBrokersImportService(this.parserMock.Object, loggerMock.Object);
    }

    [Fact]
    public async Task ParseAllAsync_ClassifiesDeposit()
    {
        List<IBKRTransactionRecord> records = new()
        {
            Record("Deposit", "Deposit", string.Empty, "0", "EUR", "5000.00"),
        };
        this.Setup(records);

        (IEnumerable<Transaction> tx, IEnumerable<AssetTransaction> assets, _) = await this.agent.ParseAllAsync("test.csv");

        Transaction transaction = Assert.Single(tx);
        Assert.Equal(TransactionCategory.DEPOSIT, transaction.Category);
        Assert.Empty(assets);
    }

    [Fact]
    public async Task ParseAllAsync_ClassifiesWithholdingAndFeesAsExpense()
    {
        List<IBKRTransactionRecord> records = new()
        {
            Record("Withholding Tax", "Withholding", "-", "0", "USD", "-15.00"),
            Record("Fee", "Commission", "-", "0", "USD", "-2.50"),
        };
        this.Setup(records);

        (IEnumerable<Transaction> tx, _, _) = await this.agent.ParseAllAsync("test.csv");

        List<Transaction> list = tx.ToList();
        Assert.Equal(2, list.Count);
        Assert.All(list, t => Assert.Equal(TransactionCategory.EXPENSE, t.Category));
    }

    [Fact]
    public async Task ParseAllAsync_ClassifiesDividendAndCreditInterestAsIncome()
    {
        List<IBKRTransactionRecord> records = new()
        {
            Record("Dividend", "AAPL dividend", "-", "0", "USD", "100.00"),
            Record("Credit Interest", "Interest on cash", "-", "0", "USD", "5.00"),
        };
        this.Setup(records);

        (IEnumerable<Transaction> tx, _, _) = await this.agent.ParseAllAsync("test.csv");

        List<Transaction> list = tx.ToList();
        Assert.Equal(2, list.Count);
        Assert.All(list, t => Assert.Equal(TransactionCategory.INCOME, t.Category));
    }

    [Fact]
    public async Task ParseAllAsync_ClassifiesDebitInterestAsExpense()
    {
        List<IBKRTransactionRecord> records = new()
        {
            Record("Debit Interest", "Interest", "-", "0", "USD", "-3.00"),
        };
        this.Setup(records);

        (IEnumerable<Transaction> tx, _, _) = await this.agent.ParseAllAsync("test.csv");

        Assert.Equal(TransactionCategory.EXPENSE, tx.Single().Category);
    }

    [Fact]
    public async Task ParseAllAsync_AssetWithZeroQuantity_InfersDirectionFromAmount()
    {
        List<IBKRTransactionRecord> records = new()
        {
            Record("Buy", "Buy AAPL", "AAPL", "0", "USD", "15000.00"),
            Record("Sell", "Sell MSFT", "MSFT", "0", "USD", "-15000.00"),
        };
        this.Setup(records);

        (_, IEnumerable<AssetTransaction> assets, _) = await this.agent.ParseAllAsync("test.csv");

        List<AssetTransaction> list = assets.ToList();
        Assert.Equal(2, list.Count);
        Assert.Equal(AssetTransactionType.Sell, list[0].Type);
        Assert.Equal(TransactionCategory.INCOME, list[0].Transaction.Category);
        Assert.Equal(AssetTransactionType.Buy, list[1].Type);
    }

    [Fact]
    public async Task ParseAllAsync_Assignment_IsSell()
    {
        List<IBKRTransactionRecord> records = new()
        {
            Record("Assignment", "Assignment of AAPL shares", "AAPL", "10", "USD", "-15000.00"),
        };
        this.Setup(records);

        (_, IEnumerable<AssetTransaction> assets, _) = await this.agent.ParseAllAsync("test.csv");

        AssetTransaction asset = Assert.Single(assets);
        Assert.Equal(AssetTransactionType.Sell, asset.Type);
    }

    [Fact]
    public async Task ParseAllAsync_Exercise_IsSell()
    {
        List<IBKRTransactionRecord> records = new()
        {
            Record("Exercise", "Exercise of MSFT shares", "MSFT", "10", "USD", "15000.00"),
        };
        this.Setup(records);

        (_, IEnumerable<AssetTransaction> assets, _) = await this.agent.ParseAllAsync("test.csv");

        AssetTransaction asset = Assert.Single(assets);
        Assert.Equal(AssetTransactionType.Sell, asset.Type);
    }

    [Fact]
    public async Task ParseAllAsync_OptionWithNegativeQuantity_IsIncome()
    {
        List<IBKRTransactionRecord> records = new()
        {
            Record("Sell", "VET 16JAN26 10 C", "VET 260116C00010000", "-1", "USD", "110.00"),
        };
        this.Setup(records);

        (IEnumerable<Transaction> tx, _, _) = await this.agent.ParseAllAsync("test.csv");

        Transaction transaction = Assert.Single(tx);
        Assert.Equal(TransactionCategory.INCOME, transaction.Category);
        Assert.Equal("VET", transaction.Description);
    }

    [Fact]
    public async Task ParseCorporateActionsAsync_SellWithParenAndDotSymbol()
    {
        List<IBKRCorporateActionRecord> records = new()
        {
            new IBKRCorporateActionRecord
            {
                AssetCategory = "Stocks",
                Currency = "CAD",
                ReportDate = "2024-07-17",
                Description = "CVO.ODD.C(CAODD89D1078) Merged(Voluntary Offer Allocation) for CAD 6.18 per Share",
                Quantity = "-33",
                Proceeds = "203.94",
            },
        };
        this.parserMock
            .Setup(p => p.ParseCorporateActionsAsync(It.IsAny<string>()))
            .ReturnsAsync(records);

        IEnumerable<AssetTransaction> result = await this.agent.ParseCorporateActionsAsync("test.csv");

        AssetTransaction asset = Assert.Single(result);
        Assert.Equal("CVO", asset.Symbol);
        Assert.Equal(AssetTransactionType.Sell, asset.Type);
        Assert.Equal(TransactionCategory.INCOME, asset.Transaction.Category);
        Assert.Equal(33, asset.Quantity);
    }

    [Fact]
    public async Task ParseCorporateActionsAsync_BuyWithUnknownSymbol()
    {
        List<IBKRCorporateActionRecord> records = new()
        {
            new IBKRCorporateActionRecord
            {
                AssetCategory = "Stocks",
                Currency = "-",
                ReportDate = "2024-01-10",
                Description = "No symbol here",
                Quantity = "5",
                Proceeds = "50.00",
            },
        };
        this.parserMock
            .Setup(p => p.ParseCorporateActionsAsync(It.IsAny<string>()))
            .ReturnsAsync(records);

        IEnumerable<AssetTransaction> result = await this.agent.ParseCorporateActionsAsync("test.csv");

        AssetTransaction asset = Assert.Single(result);
        Assert.Equal("UNKNOWN", asset.Symbol);
        Assert.Equal(AssetTransactionType.Buy, asset.Type);
        Assert.Equal("EUR", asset.Transaction.Money.Currency);
    }

    [Fact]
    public async Task ParseCorporateActionsAsync_ReturnsEmpty_WhenNoRecords()
    {
        this.parserMock
            .Setup(p => p.ParseCorporateActionsAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<IBKRCorporateActionRecord>());

        IEnumerable<AssetTransaction> result = await this.agent.ParseCorporateActionsAsync("test.csv");

        Assert.Empty(result);
    }

    [Fact]
    public async Task ParseAllAsync_SkipsRecordThatThrows()
    {
        List<IBKRTransactionRecord> records = new()
        {
            Record("Buy", "AAPL", "AAPL", "10", "USD", "15000.00"),
            new IBKRTransactionRecord { Symbol = "AAPL" },
        };
        this.Setup(records);

        (_, IEnumerable<AssetTransaction> assets, _) = await this.agent.ParseAllAsync("test.csv");

        Assert.Single(assets);
    }

    private void Setup(List<IBKRTransactionRecord> records)
    {
        this.parserMock
            .Setup(p => p.ParseIBKRAsync(It.IsAny<string>()))
            .ReturnsAsync(records);
    }

    private static IBKRTransactionRecord Record(string type, string description, string symbol, string quantity, string currency, string amount)
    {
        return new IBKRTransactionRecord
        {
            Date = "2024-12-19",
            Description = description,
            TransactionType = type,
            Symbol = symbol,
            Quantity = quantity,
            PriceCurrency = currency,
            GrossAmount = amount,
            NetAmount = amount,
        };
    }
}
