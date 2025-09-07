using MyAccountingApp.Core.Repositories;
using MyAccountingApp.Domain.Entities;
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
}
