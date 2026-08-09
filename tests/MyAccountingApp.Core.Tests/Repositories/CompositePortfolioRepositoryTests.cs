using MyAccountingApp.Core.Persistence;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.ValueObjects;
using MyAccountingApp.TestUtilities.ObjectMothers;

public class CompositePortfolioRepositoryTests : IDisposable
{
    private readonly string _tempFile;

    public CompositePortfolioRepositoryTests()
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
    public void AddOrUpdate_ShouldUpdateBothRepositories()
    {
        // Arrange
        CompositePortfolioRepository repo = new CompositePortfolioRepository(this._tempFile);
        AssetTransaction tx = AssetTransactionObjectMother.CreateBuy();
        repo.AddOrUpdate(tx);

        // Act
        List<AssetTransaction> all = repo.GetAllTransactions().ToList();

        // Assert
        Assert.Single(all);
    }

    [Fact]
    public void Delete_ShouldRemoveFromBothRepositories()
    {
        // Arrange
        CompositePortfolioRepository repo = new CompositePortfolioRepository(this._tempFile);
        AssetTransaction tx = AssetTransactionObjectMother.CreateBuy();
        repo.AddOrUpdate(tx);

        // Act
        bool result = repo.Delete(tx.Transaction.Id);

        // Assert
        Assert.True(result);
        Assert.Empty(repo.GetAllTransactions());

        // Verify persisted in JSON
        JsonPortfolioRepository jsonRepo = new JsonPortfolioRepository(this._tempFile);
        Assert.Empty(jsonRepo.GetAllTransactions());
    }

    [Fact]
    public void Delete_NonExistent_ShouldReturnFalse()
    {
        CompositePortfolioRepository repo = new CompositePortfolioRepository(this._tempFile);
        bool result = repo.Delete(Guid.NewGuid());
        Assert.False(result);
    }

    [Fact]
    public void DeleteByYear_ShouldRemoveFromBothRepos()
    {
        CompositePortfolioRepository repo = new CompositePortfolioRepository(this._tempFile);
        Transaction tx2023 = new Transaction(new DateTime(2023, 6, 15), "2023", new Money(-1500, "USD"), TransactionCategory.EXPENSE);
        Transaction tx2024 = new Transaction(new DateTime(2024, 3, 10), "2024", new Money(-2000, "USD"), TransactionCategory.EXPENSE);
        AssetTransaction asset2023 = new AssetTransaction(tx2023, "AAPL", 10, AssetTransactionType.Buy);
        AssetTransaction asset2024 = new AssetTransaction(tx2024, "MSFT", 5, AssetTransactionType.Buy);
        repo.Initialize(new[] { asset2023, asset2024 });

        int removed = repo.DeleteByYear(2024);

        Assert.Equal(1, removed);
        Assert.Single(repo.GetAllTransactions());
        Assert.Equal("AAPL", repo.GetAllTransactions().First().Symbol);
        JsonPortfolioRepository jsonRepo = new JsonPortfolioRepository(this._tempFile);
        Assert.Single(jsonRepo.GetAllTransactions());
    }
}
