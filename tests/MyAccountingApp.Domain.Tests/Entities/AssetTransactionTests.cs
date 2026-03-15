using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.ValueObjects;

namespace MyAccountingApp.Domain.Tests.Entities;

public class AssetTransactionTests
{
    [Fact]
    public void Constructor_ShouldCreateAssetTransaction_WhenValidData()
    {
        DateTime date = new DateTime(2025, 8, 27);
        Money money = new Money(-1500, "EUR");
        Transaction transaction = new Transaction(date, "AAPL", money, TransactionCategory.EXPENSE);

        AssetTransaction assetTransaction = new AssetTransaction(transaction, "AAPL", 10, AssetTransactionType.Buy);

        Assert.Equal("AAPL", assetTransaction.Symbol);
        Assert.Equal(10, assetTransaction.Quantity);
        Assert.Equal(AssetTransactionType.Buy, assetTransaction.Type);
    }

    [Fact]
    public void Constructor_ShouldMakeQuantityPositive_WhenNegativeQuantityProvided()
    {
        DateTime date = new DateTime(2025, 8, 27);
        Money money = new Money(1500, "EUR");
        Transaction transaction = new Transaction(date, "AAPL", money, TransactionCategory.INCOME);

        AssetTransaction assetTransaction = new AssetTransaction(transaction, "AAPL", -10, AssetTransactionType.Sell);

        Assert.Equal(10, assetTransaction.Quantity);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenSymbolIsEmpty()
    {
        DateTime date = new DateTime(2025, 8, 27);
        Money money = new Money(-1500, "EUR");
        Transaction transaction = new Transaction(date, "AAPL", money, TransactionCategory.EXPENSE);

        Assert.Throws<ArgumentException>(() =>
            new AssetTransaction(transaction, "", 10, AssetTransactionType.Buy));
    }

    [Fact]
    public void UnitaryCost_ShouldCalculateCorrectly()
    {
        DateTime date = new DateTime(2025, 8, 27);
        Money money = new Money(-1500, "EUR");
        Transaction transaction = new Transaction(date, "AAPL", money, TransactionCategory.EXPENSE);

        AssetTransaction assetTransaction = new AssetTransaction(transaction, "AAPL", 10, AssetTransactionType.Buy);

        Money unitaryCost = assetTransaction.UnitaryCost();

        Assert.Equal(150, unitaryCost.Amount);
        Assert.Equal("EUR", unitaryCost.Currency);
    }
}
