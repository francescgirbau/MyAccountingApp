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
        ImportService service = new(broker, txRepo, pfRepo, validator, logger);

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
        ImportService service = new(broker, txRepo, pfRepo, validator, logger);

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
        ImportService service = new(broker, txRepo, pfRepo, validator, logger);

        ImportResult result = await service.ImportFromFoldersAsync(new[] { dir });

        Assert.Empty(result.Transactions);
        Assert.Single(result.AssetTransactions);
        Assert.Equal(1, result.FilesProcessed);
        Assert.Single(pfRepo.GetAllTransactions());
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
            "",
            new Money(100, "EUR"),
            TransactionCategory.INCOME);

        FakeBroker broker = new();
        broker.Transactions = new[] { invalidTx };
        broker.AssetTransactions = Array.Empty<AssetTransaction>();
        FakeTxRepo txRepo = new();
        FakePfRepo pfRepo = new();
        TransactionValidator validator = new();
        FakeLogger<ImportService> logger = new();
        ImportService service = new(broker, txRepo, pfRepo, validator, logger);

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
        ImportService service = new(broker, txRepo, pfRepo, validator, logger);

        ImportResult result = await service.ImportFromFoldersAsync(new[] { dir });

        Assert.Equal(0, result.FilesProcessed);
        Assert.Single(result.Errors);
        Assert.Contains("Error processing", result.Errors[0]);
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
        public bool ThrowOnParse { get; set; }

        public Task<(IEnumerable<Transaction>, IEnumerable<AssetTransaction>)> ParseAllAsync(
            string filePath, CancellationToken cancellationToken = default)
        {
            if (this.ThrowOnParse)
            {
                throw new InvalidOperationException("Broker error");
            }

            return Task.FromResult((this.Transactions, this.AssetTransactions));
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
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
        }
    }

    private sealed class FakeTxRepo : ITransactionRepository
    {
        private readonly List<Transaction> _transactions = new();

        public void AddOrUpdate(Transaction tx) => this._transactions.Add(tx);
        public void Initialize(IEnumerable<Transaction> transactions)
        {
            this._transactions.Clear();
            this._transactions.AddRange(transactions);
        }

        public IEnumerable<Transaction> GetAll() => this._transactions;
        public bool Delete(Transaction transaction) => this._transactions.Remove(transaction);
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
    }
}
