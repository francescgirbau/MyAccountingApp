using MyAccountingApp.Core.Repositories;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.ValueObjects;
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

    [Fact]
    public void Delete_ShouldRemoveTransaction()
    {
        // Arrange
        InMemoryPortfolioRepository repo = new();
        AssetTransaction tx = AssetTransactionObjectMother.CreateBuy();
        repo.AddOrUpdate(tx);

        // Act
        bool result = repo.Delete(tx.Transaction.Id);

        // Assert
        Assert.True(result);
        Assert.Empty(repo.GetAllTransactions());
    }

    [Fact]
    public void Delete_NonExistent_ShouldReturnFalse()
    {
        // Arrange
        InMemoryPortfolioRepository repo = new();

        // Act
        bool result = repo.Delete(Guid.NewGuid());

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void DeleteByYear_ShouldRemoveOnlyAssetTransactionsForGivenYear()
    {
        InMemoryPortfolioRepository repo = new();
        Transaction tx2023 = new Transaction(new DateTime(2023, 6, 15), "2023", new Money(-1500, "USD"), TransactionCategory.EXPENSE);
        Transaction tx2024 = new Transaction(new DateTime(2024, 3, 10), "2024", new Money(-2000, "USD"), TransactionCategory.EXPENSE);
        AssetTransaction asset2023 = new AssetTransaction(tx2023, "AAPL", 10, AssetTransactionType.Buy);
        AssetTransaction asset2024 = new AssetTransaction(tx2024, "MSFT", 5, AssetTransactionType.Buy);
        repo.Initialize(new[] { asset2023, asset2024 });

        repo.DeleteByYear(2024);

        Assert.Single(repo.GetAllTransactions());
        Assert.Equal("AAPL", repo.GetAllTransactions().First().Symbol);
    }

    [Fact]
    public void DeleteByYear_ShouldReturnCountOfRemovedAssetTransactions()
    {
        InMemoryPortfolioRepository repo = new();
        Transaction tx2024a = new Transaction(new DateTime(2024, 3, 10), "2024a", new Money(-1500, "USD"), TransactionCategory.EXPENSE);
        Transaction tx2024b = new Transaction(new DateTime(2024, 7, 1), "2024b", new Money(-2000, "USD"), TransactionCategory.EXPENSE);
        AssetTransaction assetA = new AssetTransaction(tx2024a, "AAPL", 10, AssetTransactionType.Buy);
        AssetTransaction assetB = new AssetTransaction(tx2024b, "MSFT", 5, AssetTransactionType.Buy);
        repo.Initialize(new[] { assetA, assetB });

        int removed = repo.DeleteByYear(2024);

        Assert.Equal(2, removed);
    }
}
