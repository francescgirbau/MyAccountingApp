using MyAccountingApp.Core.Persistence;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.ValueObjects;

namespace MyAccountingApp.Core.Tests.Repositories;

public class JsonOptionTransactionRepositoryTests : IDisposable
{
    private readonly string _tempFile;

    public JsonOptionTransactionRepositoryTests()
    {
        this._tempFile = Path.GetTempFileName();
    }

    public void Dispose()
    {
        if (File.Exists(this._tempFile))
        {
            File.Delete(this._tempFile);
        }
    }

    [Fact]
    public void GetAll_ReturnsEmpty_WhenFileDoesNotExist()
    {
        JsonOptionTransactionRepository repo = new(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));

        Assert.Empty(repo.GetAll());
    }

    [Fact]
    public void GetAll_ReturnsEmpty_WhenFileIsCorrupt()
    {
        File.WriteAllText(this._tempFile, "not valid json {{{");
        JsonOptionTransactionRepository repo = new(this._tempFile);

        Assert.Empty(repo.GetAll());
    }

    [Fact]
    public void Add_ThenGetAll_RoundTrips()
    {
        JsonOptionTransactionRepository repo = new(this._tempFile);
        repo.Add(CreateOption());

        OptionTransaction loaded = Assert.Single(repo.GetAll());
        Assert.Equal("AAPL", loaded.Symbol);
        Assert.Equal(AssetTransactionType.Buy, loaded.Type);
    }

    [Fact]
    public void Update_ReplacesExistingTransaction()
    {
        JsonOptionTransactionRepository repo = new(this._tempFile);
        OptionTransaction original = CreateOption();
        repo.Add(original);

        OptionTransaction updated = new(
            original.Transaction,
            "MSFT",
            string.Empty,
            5,
            AssetTransactionType.Sell);
        repo.Update(updated);

        OptionTransaction loaded = Assert.Single(repo.GetAll());
        Assert.Equal("MSFT", loaded.Symbol);
        Assert.Equal(AssetTransactionType.Sell, loaded.Type);
    }

    [Fact]
    public void Update_DoesNothing_WhenIdNotFound()
    {
        JsonOptionTransactionRepository repo = new(this._tempFile);
        repo.Add(CreateOption());

        OptionTransaction other = CreateOption(new Guid("11111111-1111-1111-1111-111111111111"));
        repo.Update(other);

        Assert.Single(repo.GetAll());
    }

    [Fact]
    public void Delete_RemovesAndReturnsTrue()
    {
        JsonOptionTransactionRepository repo = new(this._tempFile);
        OptionTransaction option = CreateOption();
        repo.Add(option);

        bool deleted = repo.Delete(option.Transaction.Id);

        Assert.True(deleted);
        Assert.Empty(repo.GetAll());
    }

    [Fact]
    public void Delete_ReturnsFalse_WhenIdNotFound()
    {
        JsonOptionTransactionRepository repo = new(this._tempFile);
        repo.Add(CreateOption());

        bool deleted = repo.Delete(new Guid("11111111-1111-1111-1111-111111111111"));

        Assert.False(deleted);
        Assert.Single(repo.GetAll());
    }

    [Fact]
    public void DeleteByYear_RemovesOnlyMatchingYear()
    {
        JsonOptionTransactionRepository repo = new(this._tempFile);
        repo.Add(CreateOption(date: new DateTime(2023, 5, 1)));
        repo.Add(CreateOption(date: new DateTime(2024, 5, 1)));

        int removed = repo.DeleteByYear(2023);

        Assert.Equal(1, removed);
        Assert.Single(repo.GetAll());
    }

    [Fact]
    public void Initialize_OverwritesExistingData()
    {
        JsonOptionTransactionRepository repo = new(this._tempFile);
        repo.Add(CreateOption());

        repo.Initialize(new[] { CreateOption(date: new DateTime(2024, 1, 1)) });

        Assert.Single(repo.GetAll());
        Assert.Equal(new DateTime(2024, 1, 1), repo.GetAll().Single().Transaction.Date);
    }

    private static OptionTransaction CreateOption(Guid? id = null, DateTime? date = null)
    {
        Guid guid = id ?? Guid.NewGuid();
        DateTime txDate = date ?? new DateTime(2023, 10, 15);
        Transaction tx = new(guid, txDate, "Buy AAPL", new Money(1500, "USD"), TransactionCategory.EXPENSE);
        return new OptionTransaction(tx, "AAPL", "US0378331005", 10, AssetTransactionType.Buy);
    }
}
