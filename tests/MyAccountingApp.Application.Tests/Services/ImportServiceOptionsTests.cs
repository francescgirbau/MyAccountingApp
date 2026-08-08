using Microsoft.Extensions.Logging;
using MyAccountingApp.Application.Interfaces;
using MyAccountingApp.Application.Services;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Interfaces;
using MyAccountingApp.Domain.ValueObjects;

namespace MyAccountingApp.Application.Tests.Services;

public class ImportServiceOptionsTests
{
    [Fact]
    public async Task ImportFromFoldersAsync_PersistsOptionTransactions()
    {
        string dir = CreateTempDir();
        File.WriteAllText(Path.Combine(dir, "options.csv"), "dummy");

        Transaction tx = new(Guid.NewGuid(), new DateTime(2024, 5, 1), "VET 16JAN26 10 C", new Money(110, "USD"), TransactionCategory.EXPENSE);
        OptionTransaction optionTx = new(tx, "VET", string.Empty, 1, AssetTransactionType.Buy);

        FakeBroker broker = new() { OptionTransactions = new[] { optionTx } };
        FakeTxRepo txRepo = new();
        FakePfRepo pfRepo = new();
        FakeOptionRepo optionRepo = new();
        ImportService service = new(broker, txRepo, pfRepo, optionRepo, new TransactionValidator(), new FakeLogger<ImportService>());

        ImportResult result = await service.ImportFromFoldersAsync(new[] { dir });

        Assert.Single(result.OptionTransactions);
        Assert.Single(optionRepo.GetAll());
        Assert.Empty(txRepo.GetAll());
    }

    [Fact]
    public async Task ImportFromFoldersAsync_MergesAssets_UpdatingExistingById()
    {
        string dir = CreateTempDir();
        File.WriteAllText(Path.Combine(dir, "assets.csv"), "dummy");

        Guid id = Guid.NewGuid();
        Transaction existingTx = new(id, new DateTime(2024, 1, 1), "Old", new Money(100, "USD"), TransactionCategory.EXPENSE);
        AssetTransaction existing = new(existingTx, "AAPL", 5, AssetTransactionType.Buy);

        Transaction newTx = new(id, new DateTime(2024, 1, 1), "Updated", new Money(150, "USD"), TransactionCategory.EXPENSE);
        AssetTransaction updated = new(newTx, "AAPL", 10, AssetTransactionType.Buy);

        FakeBroker broker = new() { AssetTransactions = new[] { updated } };
        FakeTxRepo txRepo = new();
        FakePfRepo pfRepo = new();
        pfRepo.Initialize(new[] { existing });
        ImportService service = new(broker, txRepo, pfRepo, new FakeOptionRepo(), new TransactionValidator(), new FakeLogger<ImportService>());

        ImportResult result = await service.ImportFromFoldersAsync(new[] { dir });

        Assert.Single(result.AssetTransactions);
        AssetTransaction merged = Assert.Single(pfRepo.GetAllTransactions());
        Assert.Equal(10, merged.Quantity);
        Assert.Equal("Updated", merged.Transaction.Description);
    }

    [Fact]
    public async Task ImportFromFoldersAsync_ProcessesMultipleFolders()
    {
        string dir1 = CreateTempDir();
        File.WriteAllText(Path.Combine(dir1, "a.csv"), "dummy");
        string dir2 = CreateTempDir();
        File.WriteAllText(Path.Combine(dir2, "b.csv"), "dummy");

        Transaction tx1 = new(Guid.NewGuid(), new DateTime(2024, 2, 1), "One", new Money(10, "EUR"), TransactionCategory.INCOME);
        Transaction tx2 = new(Guid.NewGuid(), new DateTime(2024, 2, 2), "Two", new Money(20, "EUR"), TransactionCategory.INCOME);

        FakeBroker broker = new();
        broker.Transactions = new[] { tx1 };
        broker.TransactionsByFile = new Dictionary<string, IEnumerable<Transaction>>
        {
            { Path.Combine(dir2, "b.csv"), new[] { tx2 } },
        };

        FakeTxRepo txRepo = new();
        ImportService service = new(broker, txRepo, new FakePfRepo(), new FakeOptionRepo(), new TransactionValidator(), new FakeLogger<ImportService>());

        ImportResult result = await service.ImportFromFoldersAsync(new[] { dir1, dir2 });

        Assert.Equal(2, result.FilesProcessed);
        Assert.Equal(2, txRepo.GetAll().Count());
    }

    private static string CreateTempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        return dir;
    }

    private sealed class FakeBroker : IBrokerImportService
    {
        public IEnumerable<Transaction> Transactions { get; set; } = Array.Empty<Transaction>();
        public IEnumerable<AssetTransaction> AssetTransactions { get; set; } = Array.Empty<AssetTransaction>();
        public IEnumerable<OptionTransaction> OptionTransactions { get; set; } = Array.Empty<OptionTransaction>();
        public Dictionary<string, IEnumerable<Transaction>> TransactionsByFile { get; set; } = new();

        public Task<(IEnumerable<Transaction>, IEnumerable<AssetTransaction>, IEnumerable<OptionTransaction>)> ParseAllAsync(
            string filePath, CancellationToken cancellationToken = default)
        {
            IEnumerable<Transaction> tx = this.TransactionsByFile.TryGetValue(filePath, out IEnumerable<Transaction>? specific)
                ? specific
                : this.Transactions;
            return Task.FromResult((tx, this.AssetTransactions, this.OptionTransactions));
        }

        public Task<IEnumerable<AssetTransaction>> ParseCorporateActionsAsync(
            string filePath, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(this.AssetTransactions);
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
        public int DeleteByYear(int year) => this._transactions.RemoveAll(t => t.Date.Year == year);
    }

    private sealed class FakePfRepo : IPortfolioRepository
    {
        private readonly List<AssetTransaction> _transactions = new();

        public void AddOrUpdate(AssetTransaction tx) => this._transactions.Add(tx);
        public IEnumerable<AssetTransaction> GetAssetTransactions(string symbol) => this._transactions.Where(t => t.Symbol == symbol);
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

    private sealed class FakeLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
        }
    }
}
