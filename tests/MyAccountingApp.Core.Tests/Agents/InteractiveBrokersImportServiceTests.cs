using Microsoft.Extensions.Logging;
using Moq;
using MyAccountingApp.Core.Agents;
using MyAccountingApp.Core.Models;
using MyAccountingApp.Core.Services;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.ValueObjects;

namespace MyAccountingApp.Core.Tests.Agents;

public class InteractiveBrokersImportServiceTests
{
    private readonly Mock<ICsvParser> parserMock = new Mock<ICsvParser>();
    private readonly Mock<ILogger<InteractiveBrokersImportService>> loggerMock = new Mock<ILogger<InteractiveBrokersImportService>>();
    private readonly InteractiveBrokersImportService agent;

    public InteractiveBrokersImportServiceTests()
    {
        this.agent = new InteractiveBrokersImportService(
            this.parserMock.Object,
            this.loggerMock.Object);
    }

    [Fact]
    public async Task ParseAllAsync_ReturnsAssetTransactions_WhenRecordsHaveSymbol()
    {
        List<IBKRTransactionRecord> records = new List<IBKRTransactionRecord>
        {
            new IBKRTransactionRecord
            {
                Date = "2024-12-19",
                Description = "Buy 100 AAPL",
                Symbol = "AAPL",
                Quantity = "100",
                Price = "150.00",
                PriceCurrency = "USD",
                GrossAmount = "-15000.00",
                NetAmount = "-15000.00",
            },
            new IBKRTransactionRecord
            {
                Date = "2024-12-18",
                Description = "Sell 50 MSFT",
                Symbol = "MSFT",
                Quantity = "-50",
                Price = "300.00",
                PriceCurrency = "USD",
                GrossAmount = "15000.00",
                NetAmount = "15000.00",
            },
        };

        this.parserMock
            .Setup(p => p.ParseIBKRAsync(It.IsAny<string>()))
            .ReturnsAsync(records);

        (IEnumerable<Transaction> transactions, IEnumerable<AssetTransaction> assetTransactions) =
            await this.agent.ParseAllAsync("test.csv");

        Assert.Empty(transactions);
        Assert.Equal(2, assetTransactions.Count());
    }

    [Fact]
    public async Task ParseAllAsync_ReturnsTransactions_WhenRecordsHaveNoSymbol()
    {
        List<IBKRTransactionRecord> records = new List<IBKRTransactionRecord>
        {
            new IBKRTransactionRecord
            {
                Date = "2024-12-19",
                Description = "Dividend",
                TransactionType = "Dividend",
                Symbol = "-",
                Quantity = "0",
                PriceCurrency = "USD",
                GrossAmount = "100.00",
                NetAmount = "100.00",
            },
            new IBKRTransactionRecord
            {
                Date = "2024-12-18",
                Description = "Deposit",
                TransactionType = "Deposit",
                Symbol = string.Empty,
                Quantity = "0",
                PriceCurrency = "EUR",
                GrossAmount = "5000.00",
                NetAmount = "5000.00",
            },
        };

        this.parserMock
            .Setup(p => p.ParseIBKRAsync(It.IsAny<string>()))
            .ReturnsAsync(records);

        (IEnumerable<Transaction> transactions, IEnumerable<AssetTransaction> assetTransactions) =
            await this.agent.ParseAllAsync("test.csv");

        Assert.Empty(assetTransactions);
        Assert.Equal(2, transactions.Count());
    }

    [Fact]
    public async Task ParseAllAsync_ReturnsEmpty_WhenNoRecords()
    {
        this.parserMock
            .Setup(p => p.ParseIBKRAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<IBKRTransactionRecord>());

        (IEnumerable<Transaction> transactions, IEnumerable<AssetTransaction> assetTransactions) =
            await this.agent.ParseAllAsync("test.csv");

        Assert.Empty(transactions);
        Assert.Empty(assetTransactions);
    }

    [Fact]
    public async Task ParseAllAsync_Throws_WhenFilePathIsEmpty()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            this.agent.ParseAllAsync(string.Empty));
    }

    [Fact]
    public async Task ParseAllAsync_Throws_WhenFilePathIsNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            this.agent.ParseAllAsync(null!));
    }

    [Fact]
    public async Task ParseCorporateActionsAsync_ReturnsAssetTransactions()
    {
        List<IBKRCorporateActionRecord> records = new List<IBKRCorporateActionRecord>
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

        Assert.Single(result);
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
    public async Task ParseCorporateActionsAsync_Throws_WhenFilePathIsEmpty()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            this.agent.ParseCorporateActionsAsync(string.Empty));
    }

    [Fact]
    public async Task ParseAllAsync_WithOptionRecords_MapsToTransaction()
    {
        List<IBKRTransactionRecord> records = new List<IBKRTransactionRecord>
        {
            new IBKRTransactionRecord
            {
                Date = "2024-12-18",
                Description = "VET 16JAN26 10 C",
                TransactionType = "Buy",
                Symbol = "VET   260116C00010000",
                Quantity = "1.0",
                Price = "1.14",
                PriceCurrency = "USD",
                GrossAmount = "-110.11944",
                NetAmount = "-110.403383942",
            },
        };

        this.parserMock
            .Setup(p => p.ParseIBKRAsync(It.IsAny<string>()))
            .ReturnsAsync(records);

        (IEnumerable<Transaction> transactions, IEnumerable<AssetTransaction> assetTransactions) =
            await this.agent.ParseAllAsync("test.csv");

        Assert.Single(transactions);
        Assert.Empty(assetTransactions);

        Transaction tx = transactions.First();
        Assert.Equal("VET", tx.Description);
        Assert.Equal(110.4m, Math.Round(tx.Money.Amount, 1));
    }
}
