using MyAccountingApp.Core.Repositories;
using MyAccountingApp.Domain.Entities;
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
}
