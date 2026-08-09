using MyAccountingApp.Core.Persistence;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.ValueObjects;
using MyAccountingApp.TestUtilities.ObjectMothers;

public class JsonPortfolioRepositoryTests : IDisposable
{
    private readonly string _tempFile;

    public JsonPortfolioRepositoryTests()
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
    public void AddOrUpdate_ShouldPersistToJson()
    {
        // Arrange
        JsonPortfolioRepository repo = new JsonPortfolioRepository(this._tempFile);

        AssetTransaction tx = AssetTransactionObjectMother.CreateBuy();
        repo.AddOrUpdate(tx);

        // Act
        List<AssetTransaction> all = repo.GetAllTransactions().ToList();

        // Assert
        Assert.Single(all);
        Assert.Equal(tx.Symbol, all.First().Symbol);
    }

    [Fact]
    public void Initialize_ShouldLoadTransactionsFromJson()
    {
        // Arrange
        AssetTransaction tx1 = AssetTransactionObjectMother.CreateBuy();
        AssetTransaction tx2 = AssetTransactionObjectMother.CreateSell();

        JsonPortfolioRepository repo = new JsonPortfolioRepository(this._tempFile);
        repo.Initialize(new[] { tx1, tx2 });

        // Act
        JsonPortfolioRepository repo2 = new JsonPortfolioRepository(this._tempFile);
        List<AssetTransaction> all = repo2.GetAllTransactions().ToList();

        Assert.Equal(2, all.Count);
    }

    [Fact]
    public void Delete_ShouldRemoveFromJson()
    {
        // Arrange
        JsonPortfolioRepository repo = new JsonPortfolioRepository(this._tempFile);
        AssetTransaction tx = AssetTransactionObjectMother.CreateBuy();
        repo.AddOrUpdate(tx);

        // Act
        bool result = repo.Delete(tx.Transaction.Id);

        // Assert
        Assert.True(result);
        Assert.Empty(repo.GetAllTransactions());

        // Verify persisted
        JsonPortfolioRepository repo2 = new JsonPortfolioRepository(this._tempFile);
        Assert.Empty(repo2.GetAllTransactions());
    }

    [Fact]
    public void Delete_NonExistent_ShouldReturnFalse()
    {
        JsonPortfolioRepository repo = new JsonPortfolioRepository(this._tempFile);
        bool result = repo.Delete(Guid.NewGuid());

        Assert.False(result);
    }

    [Fact]
    public void DeleteByYear_ShouldRemoveAssetTransactionsForGivenYearFromFile()
    {
        JsonPortfolioRepository repo = new JsonPortfolioRepository(this._tempFile);
        Transaction tx2023 = new Transaction(new DateTime(2023, 6, 15), "2023", new Money(-1500, "USD"), TransactionCategory.EXPENSE);
        Transaction tx2024 = new Transaction(new DateTime(2024, 3, 10), "2024", new Money(-2000, "USD"), TransactionCategory.EXPENSE);
        AssetTransaction asset2023 = new AssetTransaction(tx2023, "AAPL", 10, AssetTransactionType.Buy);
        AssetTransaction asset2024 = new AssetTransaction(tx2024, "MSFT", 5, AssetTransactionType.Buy);
        repo.Initialize(new[] { asset2023, asset2024 });

        repo.DeleteByYear(2024);

        JsonPortfolioRepository reloaded = new JsonPortfolioRepository(this._tempFile);
        List<AssetTransaction> all = reloaded.GetAllTransactions().ToList();
        Assert.Single(all);
        Assert.Equal("AAPL", all[0].Symbol);
    }
}
