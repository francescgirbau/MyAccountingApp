namespace MyAccountingApp.Application.Tests.Services;

using System.Text.Json;
using MyAccountingApp.Application.Services;
using MyAccountingApp.Contracts;
using MyAccountingApp.Core.Vault;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Interfaces;
using MyAccountingApp.Domain.ValueObjects;
using MyAccountingApp.TestUtilities.Fakes;

public class IBKRRebuildServiceTests
{
    [Fact]
    public void Preview_CountsOnlyIbkrRowsAndOrphanOptionsInRequestedYears()
    {
        // Arrange
        (IBKRRebuildService service, _, _, _) = CreateService(Seed());

        // Act
        IBKRRebuildPreviewDto preview = service.Preview(new[] { 2024 });

        // Assert
        Assert.Equal(1, preview.Transactions);
        Assert.Equal(1, preview.Assets);
        Assert.Equal(2, preview.Options);
        Assert.Equal(1, preview.OptionsWithoutSource);

        IBKRRebuildYearDto year2024 = Assert.Single(preview.Years, y => y.Year == 2024);
        Assert.Equal(1, year2024.Transactions);
        Assert.Equal(1, year2024.Assets);
        Assert.Equal(2, year2024.Options);
        Assert.Equal(1, year2024.OptionsWithoutSource);
    }

    [Fact]
    public void Preview_ForYearWithoutRows_ReturnsZeroCounts()
    {
        // Arrange
        (IBKRRebuildService service, _, _, _) = CreateService(Seed());

        // Act
        IBKRRebuildPreviewDto preview = service.Preview(new[] { 2023 });

        // Assert
        Assert.Equal(0, preview.Transactions);
        Assert.Equal(0, preview.Assets);
        Assert.Equal(0, preview.Options);
        Assert.Equal(0, preview.OptionsWithoutSource);
        IBKRRebuildYearDto year2023 = Assert.Single(preview.Years);
        Assert.Equal(2023, year2023.Year);
        Assert.Equal(0, year2023.Transactions);
    }

    [Fact]
    public void Rebuild_DeletesOnlyIbkrAndOrphanRowsAndWritesBackup()
    {
        // Arrange
        string backupDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        (IBKRRebuildService service, FakeTransactionRepo txRepo, FakePortfolioRepo portfolioRepo, FakeOptionRepository optionRepo) =
            CreateService(Seed(), backupDir);

        // Act
        IBKRRebuildResultDto result = service.Rebuild(new[] { 2024 });

        // Assert: deleted counts
        Assert.Equal(1, result.TransactionsDeleted);
        Assert.Equal(1, result.AssetsDeleted);
        Assert.Equal(2, result.OptionsDeleted);
        Assert.Equal(1, result.OptionsWithoutSourceDeleted);

        // Assert: kept rows (non-IBKR 2024, manual, everything from 2025)
        Assert.Equal(3, txRepo.GetAll().Count());
        Assert.DoesNotContain(txRepo.GetAll(), t => t.Description == "IBKR 2024");
        Assert.Contains(txRepo.GetAll(), t => t.Description == "degiro 2024");
        Assert.Contains(txRepo.GetAll(), t => t.Description == "manual 2024");
        Assert.Contains(txRepo.GetAll(), t => t.Description == "IBKR 2025");

        Assert.Single(portfolioRepo.GetAllTransactions());
        Assert.DoesNotContain(portfolioRepo.GetAllTransactions(), a => a.Symbol == "IBKR_ASSET");
        Assert.Contains(portfolioRepo.GetAllTransactions(), a => a.Symbol == "DEGIRO_ASSET");

        Assert.Single(optionRepo.GetAll());
        Assert.DoesNotContain(optionRepo.GetAll(), o => o.Transaction.Date.Year == 2024);
        Assert.Contains(optionRepo.GetAll(), o => o.Transaction.Description == "ZIM 17JAN25 15.16 C");

        // Assert: a backup with the pre-deletion rows exists
        Assert.NotNull(result.BackupFile);
        Assert.True(File.Exists(result.BackupFile));
        Assert.EndsWith(".json", result.BackupFile);
        string backupJson = File.ReadAllText(result.BackupFile);
        using JsonDocument doc = JsonDocument.Parse(backupJson);
        Assert.True(doc.RootElement.TryGetProperty("transactions", out JsonElement backupTransactions));
        Assert.Equal(4, backupTransactions.GetArrayLength());
        Assert.Contains("IBKR 2024", backupJson);
    }

    [Fact]
    public void Rebuild_PreservesOptionRowsWithIbkrSourceOutsideRequestedYears()
    {
        // Arrange
        (List<Transaction> tx, List<AssetTransaction> assets, List<OptionTransaction> options) = Seed();
        options.Add(Option("DGE", "DGE 18SEP26 18 C", AssetTransactionType.Buy, "U8997440_2025_2025.csv", 2025));
        (IBKRRebuildService service, _, _, FakeOptionRepository optionRepo) = CreateService((tx, assets, options));

        // Act
        IBKRRebuildResultDto result = service.Rebuild(new[] { 2024 });

        // Assert
        Assert.Equal(2, result.OptionsDeleted);
        Assert.Equal(2, optionRepo.GetAll().Count());
        Assert.Contains(optionRepo.GetAll(), o => o.Transaction.Description == "DGE 18SEP26 18 C");
    }

    private static (IBKRRebuildService Service, FakeTransactionRepo TxRepo, FakePortfolioRepo PortfolioRepo, FakeOptionRepository OptionRepo) CreateService(
        (List<Transaction> Transactions, List<AssetTransaction> Assets, List<OptionTransaction> Options) seed,
        string? backupDirectory = null)
    {
        FakeTransactionRepo txRepo = new();
        foreach (Transaction tx in seed.Transactions)
        {
            txRepo.AddOrUpdate(tx);
        }

        FakePortfolioRepo portfolioRepo = new();
        foreach (AssetTransaction asset in seed.Assets)
        {
            portfolioRepo.AddOrUpdate(asset);
        }

        FakeOptionRepository optionRepo = new();
        foreach (OptionTransaction option in seed.Options)
        {
            optionRepo.Add(option);
        }

        string dir = backupDirectory ?? Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        IBKRRebuildService service = new(txRepo, portfolioRepo, optionRepo, new DisabledVaultService(), dir);
        return (service, txRepo, portfolioRepo, optionRepo);
    }

    private static (List<Transaction> Transactions, List<AssetTransaction> Assets, List<OptionTransaction> Options) Seed()
    {
        return (
            new List<Transaction>
            {
                Tx(2024, "IBKR 2024", "U8997440_2024_2024.csv"),
                Tx(2024, "degiro 2024", "degiro.csv"),
                Tx(2024, "manual 2024", null),
                Tx(2025, "IBKR 2025", "U8997440_2025_2025.csv"),
            },
            new List<AssetTransaction>
            {
                Asset("IBKR_ASSET", "U8997440_2024_2024.csv", 2024),
                Asset("DEGIRO_ASSET", "degiro.csv", 2024),
            },
            new List<OptionTransaction>
            {
                Option("EC", "EC 16MAY25 10 P", AssetTransactionType.Sell, "U8997440_2024_2024.csv", 2024),
                Option("EC", "EC 16MAY25 10 P", AssetTransactionType.Sell, null, 2024),
                Option("ZIM", "ZIM 17JAN25 15.16 C", AssetTransactionType.Buy, null, 2025),
            });
    }

    private static Transaction Tx(int year, string description, string? source)
    {
        return new Transaction(new DateTime(year, 6, 15), description, new Money(100m, "EUR"), TransactionCategory.DEPOSIT, source);
    }

    private static AssetTransaction Asset(string symbol, string? source, int year)
    {
        Transaction transaction = new(new DateTime(year, 6, 15), symbol, new Money(100m, "EUR"), TransactionCategory.INVESTMENT);
        AssetTransaction asset = new(transaction, symbol, 10m, AssetTransactionType.Buy);
        asset.SetSource(source);
        return asset;
    }

    private static OptionTransaction Option(string symbol, string description, AssetTransactionType type, string? source, int year)
    {
        Transaction transaction = new(new DateTime(year, 6, 15), description, new Money(100m, "EUR"), TransactionCategory.DIVESTMENT);
        OptionTransaction option = new(transaction, symbol, "IE0000000000", 1m, type);
        transaction.SetSource(source);
        return option;
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

        public void Initialize(IEnumerable<AssetTransaction> assetTransactions)
        {
            this._rows.Clear();
            this._rows.AddRange(assetTransactions);
        }
    }
}