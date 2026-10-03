namespace MyAccountingApp.Application.Tests.Services;

using MyAccountingApp.Application.Services;
using MyAccountingApp.Contracts;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Interfaces;
using MyAccountingApp.Domain.ValueObjects;
using MyAccountingApp.TestUtilities.Fakes;

public class IBKRDataHealthServiceTests
{
    [Fact]
    public void GetReport_CountsRowsByYearAndSource()
    {
        // Arrange
        Transaction[] transactions =
        {
            Tx(2024, 5, 1, "deposit", 1000m, TransactionCategory.DEPOSIT, "U8997440_2024_2024.csv"),
            Tx(2025, 6, 1, "dividend", 50m, TransactionCategory.DIVIDEND, "U8997440_2025_2025.csv"),
            Tx(2025, 7, 1, "manual", 20m, TransactionCategory.EXPENSE, null),
        };
        AssetTransaction[] assets =
        {
            Asset("AAPL", 10m, AssetTransactionType.Buy, TransactionCategory.INVESTMENT, "U8997440_2024_2024.csv", 2024),
            Asset("MSFT", 5m, AssetTransactionType.Sell, TransactionCategory.DIVESTMENT, "degiro.csv", 2025),
        };
        OptionTransaction[] options =
        {
            Option("EC", "EC 16MAY25 10 P", 1m, AssetTransactionType.Sell, TransactionCategory.DIVESTMENT, 2024),
        };
        IBKRDataHealthService service = CreateService(transactions, assets, options);

        // Act
        IBKRDataHealthDto report = service.GetReport();

        // Assert
        Assert.Equal(3, report.TotalTransactions);
        Assert.Equal(2, report.IbkrTransactions);
        Assert.Equal(2, report.TotalAssets);
        Assert.Equal(1, report.IbkrAssets);
        Assert.Equal(1, report.TotalOptions);
        Assert.Equal(0, report.IbkrOptions);

        IBKRYearSummaryDto year2024 = Assert.Single(report.Years, y => y.Year == 2024);
        Assert.Equal(1, year2024.Transactions);
        Assert.Equal(1, year2024.Assets);
        Assert.Equal(1, year2024.Options);
        Assert.Equal(1, year2024.IbkrTransactions);
        Assert.Equal(1, year2024.IbkrAssets);
        Assert.Equal(0, year2024.IbkrOptions);

        IBKRYearSummaryDto year2025 = Assert.Single(report.Years, y => y.Year == 2025);
        Assert.Equal(2, year2025.Transactions);
        Assert.Equal(1, year2025.Assets);
        Assert.Equal(0, year2025.Options);
        Assert.Equal(1, year2025.IbkrTransactions);
        Assert.Equal(0, year2025.IbkrAssets);
        Assert.Equal(0, year2025.IbkrOptions);

        IBKRSourcesSummaryDto ibkr2024 = Assert.Single(report.Sources, s => s.Source == "U8997440_2024_2024.csv");
        Assert.Equal(1, ibkr2024.Transactions);
        Assert.Equal(1, ibkr2024.Assets);
        Assert.Equal(0, ibkr2024.Options);

        IBKRSourcesSummaryDto noSource = Assert.Single(report.Sources, s => s.Source == "(no source)");
        Assert.Equal(1, noSource.Transactions);
        Assert.Equal(0, noSource.Assets);
        Assert.Equal(1, noSource.Options);
    }

    [Fact]
    public void GetReport_FlagsExpiredContractWithoutClosingLeg()
    {
        // Arrange
        OptionTransaction[] options =
        {
            Option("EC", "EC 16MAY25 10 P", 1m, AssetTransactionType.Sell, TransactionCategory.DIVESTMENT, 2024, 5, 22),
            Option("DGE", "DGE 21JUN24 27 P", 1m, AssetTransactionType.Sell, TransactionCategory.DIVESTMENT, 2024, 1, 10),
            Option("DGE", "DGE 21JUN24 27 P", 1m, AssetTransactionType.Buy, TransactionCategory.INVESTMENT, 2024, 6, 21),
        };
        IBKRDataHealthService service = CreateService(Array.Empty<Transaction>(), Array.Empty<AssetTransaction>(), options);

        // Act
        IBKRDataHealthDto report = service.GetReport();

        // Assert
        IBKROpenExpiredContractDto flagged = Assert.Single(report.OpenExpiredContracts);
        Assert.Equal("EC 16MAY25 10 P", flagged.Description);
        Assert.Equal("Short Put", flagged.Strategy);
        Assert.Equal("Put", flagged.Side);
        Assert.Equal(-1m, flagged.Quantity);
        Assert.Equal(new DateOnly(2025, 5, 16), flagged.Expiration);
        Assert.Equal(1, flagged.Legs);
    }

    [Fact]
    public void GetReport_FlagsPhantomAssetCandidates()
    {
        // Arrange
        AssetTransaction[] assets =
        {
            Asset("X", 1m, AssetTransactionType.Buy, TransactionCategory.DIVIDEND, "U8997440_2024_2024.csv", 2024),
            Asset("Y", 1m, AssetTransactionType.Buy, TransactionCategory.INVESTMENT, null, 2024),
            Asset("Z", 100m, AssetTransactionType.Sell, TransactionCategory.DIVESTMENT, null, 2024),
        };
        IBKRDataHealthService service = CreateService(Array.Empty<Transaction>(), assets, Array.Empty<OptionTransaction>());

        // Act
        IBKRDataHealthDto report = service.GetReport();

        // Assert
        IBKRPhantomAssetDto phantom = Assert.Single(report.PhantomAssets);
        Assert.Equal("X", phantom.Symbol);
        Assert.Equal(1m, phantom.Quantity);
        Assert.Equal("DIVIDEND", phantom.Category);
        Assert.Equal("U8997440_2024_2024.csv", phantom.Source);
    }

    private static IBKRDataHealthService CreateService(
        IEnumerable<Transaction> transactions,
        IEnumerable<AssetTransaction> assets,
        IEnumerable<OptionTransaction> options)
    {
        FakeTransactionRepo txRepo = new();
        foreach (Transaction tx in transactions)
        {
            txRepo.AddOrUpdate(tx);
        }

        FakePortfolioRepo portfolioRepo = new();
        foreach (AssetTransaction asset in assets)
        {
            portfolioRepo.AddOrUpdate(asset);
        }

        FakeOptionRepository optionRepo = new();
        foreach (OptionTransaction option in options)
        {
            optionRepo.Add(option);
        }

        return new IBKRDataHealthService(txRepo, portfolioRepo, optionRepo);
    }

    private static Transaction Tx(int year, int month, int day, string description, decimal amount, TransactionCategory category, string? source)
    {
        return new Transaction(new DateTime(year, month, day), description, new Money(amount, "EUR"), category, source);
    }

    private static AssetTransaction Asset(string symbol, decimal quantity, AssetTransactionType type, TransactionCategory category, string? source, int year, int month = 1, int day = 1)
    {
        Transaction transaction = Tx(year, month, day, symbol, 100m, category, null);
        AssetTransaction asset = new(transaction, symbol, quantity, type);
        asset.SetSource(source);
        return asset;
    }

    private static OptionTransaction Option(string symbol, string description, decimal quantity, AssetTransactionType type, TransactionCategory category, int year, int month = 1, int day = 1)
    {
        Transaction transaction = Tx(year, month, day, description, 100m, category, null);
        return new OptionTransaction(transaction, symbol, string.Empty, quantity, type);
    }

    private sealed class FakeTransactionRepo : ITransactionRepository
    {
        private readonly List<Transaction> _rows = new();

        public IEnumerable<Transaction> GetAll()
        {
            return this._rows;
        }

        public void AddOrUpdate(Transaction transaction)
        {
            this._rows.RemoveAll(t => t.Id == transaction.Id);
            this._rows.Add(transaction);
        }

        public bool Delete(Transaction transaction)
        {
            return this._rows.RemoveAll(t => t.Id == transaction.Id) > 0;
        }

        public int DeleteByYear(int year)
        {
            return this._rows.RemoveAll(t => t.Date.Year == year);
        }

        public void Initialize(IEnumerable<Transaction> transactions)
        {
            this._rows.Clear();
            this._rows.AddRange(transactions);
        }
    }

    private sealed class FakePortfolioRepo : IPortfolioRepository
    {
        private readonly List<AssetTransaction> _rows = new();

        public IEnumerable<AssetTransaction> GetAllTransactions()
        {
            return this._rows;
        }

        public IEnumerable<AssetTransaction> GetAssetTransactions(string symbol)
        {
            return this._rows.Where(a => a.Symbol == symbol);
        }

        public void AddOrUpdate(AssetTransaction assetTransaction)
        {
            this._rows.RemoveAll(a => a.Transaction.Id == assetTransaction.Transaction.Id);
            this._rows.Add(assetTransaction);
        }

        public bool Delete(Guid transactionId)
        {
            return this._rows.RemoveAll(a => a.Transaction.Id == transactionId) > 0;
        }

        public int DeleteByYear(int year)
        {
            return this._rows.RemoveAll(a => a.Transaction.Date.Year == year);
        }

        public void Initialize(IEnumerable<AssetTransaction> transactions)
        {
            this._rows.Clear();
            this._rows.AddRange(transactions);
        }
    }
}