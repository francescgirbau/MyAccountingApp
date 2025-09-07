using MyAccountingApp.Core.Repositories;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.TestUtilities.ObjectMothers;

public class InMemoryPortfolioRepositoryTests
{
    [Fact]
    public void Initialize_WithTransactions_ShouldStoreThem()
    {
        // Arrange
        InMemoryPortfolioRepository repo = new();
        AssetTransaction tx1 = AssetTransactionObjectMother.CreateBuy();
        AssetTransaction tx2 = AssetTransactionObjectMother.CreateSell();

        repo.Initialize(new[] { tx1, tx2 });

        // Act
        List<AssetTransaction> all = repo.GetAllTransactions().ToList();

        // Assert
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public void AddOrUpdate_ShouldAddTransaction()
    {
        // Arrange
        InMemoryPortfolioRepository repo = new();
        AssetTransaction tx = AssetTransactionObjectMother.CreateBuy();

        repo.AddOrUpdate(tx);

        // Act
        List<AssetTransaction> all = repo.GetAllTransactions().ToList();

        // Assert
        Assert.Single(all);
        Assert.Equal(tx.Symbol, all.First().Symbol);
    }

    [Fact]
    public void GetAssetTransactions_ShouldFilterBySymbol()
    {
        // Arrange
        InMemoryPortfolioRepository repo = new();
        AssetTransaction tx1 = AssetTransactionObjectMother.CreateBuy(symbol: "AAPL");
        AssetTransaction tx2 = AssetTransactionObjectMother.CreateSell(symbol: "MSFT");

        repo.Initialize(new[] { tx1, tx2 });

        // Act
        List<AssetTransaction> aaplTransactions = repo.GetAssetTransactions("AAPL").ToList();

        // Assert
        Assert.Single(aaplTransactions);
        Assert.Equal("AAPL", aaplTransactions.First().Symbol);
    }
}
