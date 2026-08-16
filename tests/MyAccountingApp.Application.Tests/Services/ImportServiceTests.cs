using Microsoft.Extensions.Logging;
using MyAccountingApp.Application.Interfaces;
using MyAccountingApp.Application.Services;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Interfaces;
using MyAccountingApp.Domain.ValueObjects;

namespace MyAccountingApp.Application.Tests.Services;

public class ImportServiceTests
{
    [Fact]
    public async Task ImportFromFoldersAsync_WithFolderNotFound_ReturnsError()
    {
        FakeBroker broker = new();
        FakeTxRepo txRepo = new();
        FakePfRepo pfRepo = new();
        TransactionValidator validator = new();
        FakeLogger<ImportService> logger = new();
        ImportService service = new(broker, txRepo, pfRepo, new FakeOptionRepo(), validator, logger);

        ImportResult result = await service.ImportFromFoldersAsync(new[] { "/nonexistent/path" });

        Assert.Empty(result.Transactions);
        Assert.Empty(result.AssetTransactions);
        Assert.Single(result.Errors);
        Assert.Contains("not found", result.Errors[0]);
        Assert.Equal(0, result.FilesProcessed);
    }

    [Fact]
    public async Task ImportFromFoldersAsync_ProcessesTransactionFiles()
    {
        string dir = CreateTempDir();
        string file = Path.Combine(dir, "transactions.csv");
        File.WriteAllText(file, "dummy");

        Transaction tx = new(
            Guid.NewGuid(),
            new DateTime(2024, 1, 15),
            "Test",
            new Money(100, "EUR"),
            TransactionCategory.INCOME);

        FakeBroker broker = new();
        broker.Transactions = new[] { tx };
        broker.AssetTransactions = Array.Empty<AssetTransaction>();
        FakeTxRepo txRepo = new();
        FakePfRepo pfRepo = new();
        TransactionValidator validator = new();
        FakeLogger<ImportService> logger = new();
        ImportService service = new(broker, txRepo, pfRepo, new FakeOptionRepo(), validator, logger);

        ImportResult result = await service.ImportFromFoldersAsync(new[] { dir });

        Assert.Single(result.Transactions);
        Assert.Empty(result.AssetTransactions);
        Assert.Equal(1, result.FilesProcessed);
        Assert.Single(txRepo.GetAll());
    }

    [Fact]
    public async Task ImportFromFoldersAsync_ProcessesCorporateActions()
    {
        string dir = CreateTempDir("CORPORATE");
        string file = Path.Combine(dir, "corp.csv");
        File.WriteAllText(file, "dummy");

        Transaction tx = new(
            Guid.NewGuid(),
            new DateTime(2024, 1, 15),
            "Corp",
            new Money(500, "USD"),
            TransactionCategory.EXPENSE);

        AssetTransaction assetTx = new(tx, "AAPL", 10, AssetTransactionType.Buy);

        FakeBroker broker = new();
        broker.Transactions = Array.Empty<Transaction>();
        broker.AssetTransactions = new[] { assetTx };
        FakeTxRepo txRepo = new();
        FakePfRepo pfRepo = new();
        TransactionValidator validator = new();
        FakeLogger<ImportService> logger = new();
        ImportService service = new(broker, txRepo, pfRepo, new FakeOptionRepo(), validator, logger);

        ImportResult result = await service.ImportFromFoldersAsync(new[] { dir });

        Assert.Empty(result.Transactions);
        Assert.Single(result.AssetTransactions);
        Assert.Equal(1, result.FilesProcessed);
        Assert.Single(pfRepo.GetAllTransactions());
    }

    [Fact]
    public async Task ImportFromFoldersAsync_StampsSourceWithFileName()
    {
        string dir = CreateTempDir();
        string file = Path.Combine(dir, "accounts.csv");
        File.WriteAllText(file, "dummy");

        Transaction tx = new(
            Guid.NewGuid(),
            new DateTime(2024, 1, 15),
            "Test",
            new Money(100, "EUR"),
            TransactionCategory.INCOME);

        AssetTransaction assetTx = new(
            new Transaction(Guid.NewGuid(), new DateTime(2024, 1, 15), "Buy", new Money(200, "EUR"), TransactionCategory.EXPENSE),
            "AAPL",
            2,
            AssetTransactionType.Buy);

        FakeBroker broker = new();
        broker.Transactions = new[] { tx };
        broker.AssetTransactions = new[] { assetTx };
        FakeTxRepo txRepo = new();
        FakePfRepo pfRepo = new();
        TransactionValidator validator = new();
        FakeLogger<ImportService> logger = new();
        ImportService service = new(broker, txRepo, pfRepo, new FakeOptionRepo(), validator, logger);

        ImportResult result = await service.ImportFromFoldersAsync(new[] { dir });

        Assert.Equal("accounts.csv", tx.Source);
        Assert.Equal("accounts.csv", assetTx.Source);
        Assert.Equal("accounts.csv", result.Transactions[0].Source);
        Assert.Equal("accounts.csv", result.AssetTransactions[0].Source);
    }

    [Fact]
    public async Task ImportFromFoldersAsync_StampsCorporateActionsWithFileName()
    {
        string dir = CreateTempDir("CORPORATE");
        string file = Path.Combine(dir, "corporate.csv");
        File.WriteAllText(file, "dummy");

        AssetTransaction assetTx = new(
            new Transaction(Guid.NewGuid(), new DateTime(2024, 1, 15), "Corp", new Money(500, "USD"), TransactionCategory.EXPENSE),
            "AAPL",
            10,
            AssetTransactionType.Buy);

        FakeBroker broker = new();
        broker.Transactions = Array.Empty<Transaction>();
        broker.AssetTransactions = new[] { assetTx };
        FakeTxRepo txRepo = new();
        FakePfRepo pfRepo = new();
        TransactionValidator validator = new();
        FakeLogger<ImportService> logger = new();
        ImportService service = new(broker, txRepo, pfRepo, new FakeOptionRepo(), validator, logger);

        ImportResult result = await service.ImportFromFoldersAsync(new[] { dir });

        Assert.Equal("corporate.csv", assetTx.Source);
        Assert.Single(result.AssetTransactions);
    }

    [Fact]
    public async Task ImportFromFoldersAsync_SkipsInvalidTransactions()
    {
        string dir = CreateTempDir();
        string file = Path.Combine(dir, "bad.csv");
        File.WriteAllText(file, "dummy");

        Transaction invalidTx = new(
            Guid.NewGuid(),
            DateTime.UtcNow.AddDays(10),
            string.Empty,
            new Money(100, "EUR"),
            TransactionCategory.INCOME);

        FakeBroker broker = new();
        broker.Transactions = new[] { invalidTx };
        broker.AssetTransactions = Array.Empty<AssetTransaction>();
        FakeTxRepo txRepo = new();
        FakePfRepo pfRepo = new();
        TransactionValidator validator = new();
        FakeLogger<ImportService> logger = new();
        ImportService service = new(broker, txRepo, pfRepo, new FakeOptionRepo(), validator, logger);

        ImportResult result = await service.ImportFromFoldersAsync(new[] { dir });

        Assert.Single(result.Transactions);
        Assert.NotEmpty(result.ValidationErrors);
        Assert.Empty(txRepo.GetAll());
    }

    [Fact]
    public async Task ImportFromFoldersAsync_HandlesBrokerException()
    {
        string dir = CreateTempDir();
        string file = Path.Combine(dir, "broken.csv");
        File.WriteAllText(file, "dummy");

        FakeBroker broker = new();
        broker.ThrowOnParse = true;
        FakeTxRepo txRepo = new();
        FakePfRepo pfRepo = new();
        TransactionValidator validator = new();
        FakeLogger<ImportService> logger = new();
        ImportService service = new(broker, txRepo, pfRepo, new FakeOptionRepo(), validator, logger);

        ImportResult result = await service.ImportFromFoldersAsync(new[] { dir });

        Assert.Equal(0, result.FilesProcessed);
        Assert.Single(result.Errors);
        Assert.Contains("Error processing", result.Errors[0]);
    }

    [Fact]
    public async Task ImportFromFoldersAsync_DoesNotMatchTransferPairs()
    {
        string dir = CreateTempDir();
        string file = Path.Combine(dir, "tx.csv");
        File.WriteAllText(file, "dummy");

        Transaction expense = new(
            Guid.NewGuid(),
            new DateTime(2024, 3, 1),
            "Transferencia a FRANCESC GIRBAU IBKR",
            new Money(1000, "EUR"),
            TransactionCategory.EXPENSE);

        Transaction income = new(
            Guid.NewGuid(),
            new DateTime(2024, 3, 1),
            "Transferencia de FRANCESC a IBKR",
            new Money(1000, "EUR"),
            TransactionCategory.INCOME);

        FakeBroker broker = new();
        broker.Transactions = new[] { expense, income };
        broker.AssetTransactions = Array.Empty<AssetTransaction>();
        FakeTxRepo txRepo = new();
        FakePfRepo pfRepo = new();
        TransactionValidator validator = new();
        FakeLogger<ImportService> logger = new();
        ImportService service = new(broker, txRepo, pfRepo, new FakeOptionRepo(), validator, logger);

        ImportResult result = await service.ImportFromFoldersAsync(new[] { dir });

        var allTxs = txRepo.GetAll().ToList();
        Assert.Equal(2, allTxs.Count);
        Assert.Equal(TransactionCategory.EXPENSE, allTxs[0].Category);
        Assert.Equal(TransactionCategory.INCOME, allTxs[1].Category);
    }

    [Fact]
    public async Task ImportFromFoldersAsync_DoesNotMatchNonMatchingTransfers()
    {
        string dir = CreateTempDir();
        string file = Path.Combine(dir, "tx.csv");
        File.WriteAllText(file, "dummy");

        Transaction expense = new(
            Guid.NewGuid(),
            new DateTime(2024, 3, 1),
            "Transferencia a FRANCESC",
            new Money(1000, "EUR"),
            TransactionCategory.EXPENSE);

        Transaction income = new(
            Guid.NewGuid(),
            new DateTime(2024, 3, 2),
            "Bonus",
            new Money(500, "EUR"),
            TransactionCategory.INCOME);

        FakeBroker broker = new();
        broker.Transactions = new[] { expense, income };
        broker.AssetTransactions = Array.Empty<AssetTransaction>();
        FakeTxRepo txRepo = new();
        FakePfRepo pfRepo = new();
        TransactionValidator validator = new();
        FakeLogger<ImportService> logger = new();
        ImportService service = new(broker, txRepo, pfRepo, new FakeOptionRepo(), validator, logger);

        ImportResult result = await service.ImportFromFoldersAsync(new[] { dir });

        var allTxs = txRepo.GetAll().ToList();
        Assert.Equal(2, allTxs.Count);
        Assert.Contains(allTxs, tx => tx.Category == TransactionCategory.EXPENSE);
        Assert.Contains(allTxs, tx => tx.Category == TransactionCategory.INCOME);
    }

    [Fact]
    public async Task ImportFromFoldersAsync_ReimportingFxPair_DoesNotDuplicateLegs()
    {
        string dir = CreateTempDir();
        string file = Path.Combine(dir, "fx.csv");
        File.WriteAllText(file, "dummy");

        Guid pairId = Guid.NewGuid();
        Transaction outLeg = new(new DateTime(2022, 2, 24), "FX EUR->USD", new Money(490.24m, "EUR"), TransactionCategory.FX_CONVERSION);
        outLeg.SetFxPair(pairId, FxLeg.Out, 1.1121m, "64900984-84e2-4611-923d-958f45aa2d55");
        Transaction inLeg = new(new DateTime(2022, 2, 24), "FX EUR->USD", new Money(545.20m, "USD"), TransactionCategory.FX_CONVERSION);
        inLeg.SetFxPair(pairId, FxLeg.In, 1.1121m, "64900984-84e2-4611-923d-958f45aa2d55");

        FakeBroker broker = new();
        broker.Transactions = new[] { outLeg, inLeg };
        FakeTxRepo txRepo = new();
        TransactionValidator validator = new();
        FakeLogger<ImportService> logger = new();
        ImportService service = new(broker, txRepo, new FakePfRepo(), new FakeOptionRepo(), validator, logger);

        await service.ImportFromFoldersAsync(new[] { dir });
        await service.ImportFromFoldersAsync(new[] { dir });

        List<Transaction> all = txRepo.GetAll().ToList();
        Assert.Equal(2, all.Count);
        Assert.Single(all, t => t.FxLeg == FxLeg.Out);
        Assert.Single(all, t => t.FxLeg == FxLeg.In);
    }

    [Fact]
    public async Task ImportFromFoldersAsync_ReimportingOrdinaryTransaction_StillAppends()
    {
        string dir = CreateTempDir();
        string file = Path.Combine(dir, "tx.csv");
        File.WriteAllText(file, "dummy");

        Transaction income = new(
            Guid.NewGuid(),
            new DateTime(2024, 1, 15),
            "Test",
            new Money(100, "EUR"),
            TransactionCategory.INCOME);

        FakeBroker broker = new();
        broker.Transactions = new[] { income };
        FakeTxRepo txRepo = new();
        TransactionValidator validator = new();
        FakeLogger<ImportService> logger = new();
        ImportService service = new(broker, txRepo, new FakePfRepo(), new FakeOptionRepo(), validator, logger);

        await service.ImportFromFoldersAsync(new[] { dir });
        await service.ImportFromFoldersAsync(new[] { dir });

        Assert.Equal(2, txRepo.GetAll().Count());
    }

    private static string CreateTempDir(string suffix = "")
    {
        string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), suffix);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private sealed class FakeBroker : IBrokerImportService
    {
        public IEnumerable<Transaction> Transactions { get; set; } = Array.Empty<Transaction>();
        public IEnumerable<AssetTransaction> AssetTransactions { get; set; } = Array.Empty<AssetTransaction>();
        public IEnumerable<OptionTransaction> OptionTransactions { get; set; } = Array.Empty<OptionTransaction>();
        public bool ThrowOnParse { get; set; }

        public Task<(IEnumerable<Transaction>, IEnumerable<AssetTransaction>, IEnumerable<OptionTransaction>)> ParseAllAsync(
            string filePath, CancellationToken cancellationToken = default)
        {
            if (this.ThrowOnParse)
            {
                throw new InvalidOperationException("Broker error");
            }

            return Task.FromResult((this.Transactions, this.AssetTransactions, this.OptionTransactions));
        }

        public Task<IEnumerable<AssetTransaction>> ParseCorporateActionsAsync(
            string filePath, CancellationToken cancellationToken = default)
        {
            if (this.ThrowOnParse)
            {
                throw new InvalidOperationException("Broker error");
            }

            return Task.FromResult(this.AssetTransactions);
        }
    }

    private sealed class FakeLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
        }
    }

    private sealed class FakeTxRepo : ITransactionRepository
    {
        private readonly List<Transaction> _transactions = new();

        public void AddOrUpdate(Transaction tx)
        {
            this._transactions.RemoveAll(t => t.Id == tx.Id);
            this._transactions.Add(tx);
        }

        public void Initialize(IEnumerable<Transaction> transactions)
        {
            this._transactions.Clear();
            this._transactions.AddRange(transactions);
        }

        public IEnumerable<Transaction> GetAll() => this._transactions;
        public bool Delete(Transaction transaction) => this._transactions.Remove(transaction);
        public int DeleteByYear(int year) => this._transactions.RemoveAll(t => t.Date.Year == year);
    }

    private sealed class FakePfRepo : IPortfolioRepository
    {
        private readonly List<AssetTransaction> _transactions = new();

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

    private sealed class FakeOptionRepo : IOptionTransactionRepository
    {
        private readonly List<OptionTransaction> _transactions = new();

        public void Add(OptionTransaction tx) => this._transactions.Add(tx);
        public IEnumerable<OptionTransaction> GetAll() => this._transactions;
        public void Initialize(IEnumerable<OptionTransaction> transactions)
        {
            this._transactions.Clear();
            this._transactions.AddRange(transactions);
        }

        public void Update(OptionTransaction tx)
        {
            int index = this._transactions.FindIndex(t => t.Transaction.Id == tx.Transaction.Id);
            if (index >= 0)
            {
                this._transactions[index] = tx;
            }
        }

        public bool Delete(Guid id) => true;
        public int DeleteByYear(int year) => this._transactions.RemoveAll(t => t.Transaction.Date.Year == year);
    }
}
