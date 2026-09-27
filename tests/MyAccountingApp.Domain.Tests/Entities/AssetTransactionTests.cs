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
            new AssetTransaction(transaction, string.Empty, 10, AssetTransactionType.Buy));
    }

    [Fact]
    public void UpdateSymbol_ShouldChangeSymbol_WhenValid()
    {
        DateTime date = new DateTime(2025, 8, 27);
        Money money = new Money(-1500, "EUR");
        Transaction transaction = new Transaction(date, "AAPL", money, TransactionCategory.EXPENSE);
        AssetTransaction assetTransaction = new AssetTransaction(transaction, "AAPL", 10, AssetTransactionType.Buy);

        assetTransaction.UpdateSymbol("COBAS_INTERNACIONAL_D");

        Assert.Equal("COBAS_INTERNACIONAL_D", assetTransaction.Symbol);
    }

    [Fact]
    public void UpdateSymbol_ShouldThrow_WhenSymbolIsEmpty()
    {
        DateTime date = new DateTime(2025, 8, 27);
        Money money = new Money(-1500, "EUR");
        Transaction transaction = new Transaction(date, "AAPL", money, TransactionCategory.EXPENSE);
        AssetTransaction assetTransaction = new AssetTransaction(transaction, "AAPL", 10, AssetTransactionType.Buy);

        Assert.Throws<ArgumentException>(() => assetTransaction.UpdateSymbol(string.Empty));
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

    [Theory]
    [InlineData(1000, 0.2, 200)]
    [InlineData(2000, 0.2, 400)]
    [InlineData(10, 0.5, 5)]
    [InlineData(10, 2, 20)]
    [InlineData(15, 2, 30)]
    [InlineData(17, 0.6, 10)]
    [InlineData(3, 0.5, 2)]
    public void ApplySplitFactor_ShouldRoundToWholeShares(decimal initialQuantity, decimal factor, decimal expectedQuantity)
    {
        Transaction transaction = new Transaction(
            new DateTime(2025, 8, 27),
            "TEST",
            new Money(-100, "EUR"),
            TransactionCategory.EXPENSE);
        AssetTransaction assetTransaction = new AssetTransaction(transaction, "TEST", initialQuantity, AssetTransactionType.Buy);

        assetTransaction.ApplySplitFactor(factor);

        Assert.Equal(expectedQuantity, assetTransaction.Quantity);
    }

    [Fact]
    public void ApplySplitFactor_ShouldKeepTotalAmount_AndAdjustUnitaryCost()
    {
        // Consolidació 5:1 (cas CEQ): 1000 accions @ 0.09 → 200 @ 0.45 amb el mateix total de 90.
        Transaction transaction = new Transaction(
            new DateTime(2023, 2, 13),
            "CEQ",
            new Money(-90, "CAD"),
            TransactionCategory.EXPENSE);
        AssetTransaction assetTransaction = new AssetTransaction(transaction, "CEQ", 1000, AssetTransactionType.Buy);

        assetTransaction.ApplySplitFactor(0.2m);

        Assert.Equal(200, assetTransaction.Quantity);
        Assert.Equal(90, assetTransaction.Transaction.Money.Amount);
        Assert.Equal(0.45m, assetTransaction.UnitaryCost().Amount);
    }

    [Fact]
    public void ApplySplitFactor_ShouldIncreaseQuantity_ForForwardSplit()
    {
        Transaction transaction = new Transaction(
            new DateTime(2025, 8, 27),
            "TEST",
            new Money(-1500, "EUR"),
            TransactionCategory.EXPENSE);
        AssetTransaction assetTransaction = new AssetTransaction(transaction, "TEST", 10, AssetTransactionType.Buy);

        assetTransaction.ApplySplitFactor(2m);

        Assert.Equal(20, assetTransaction.Quantity);
        Assert.Equal(75, assetTransaction.UnitaryCost().Amount);
    }

    [Fact]
    public void ApplySplitFactor_ShouldThrow_WhenFactorIsNotPositive()
    {
        Transaction transaction = new Transaction(
            new DateTime(2025, 8, 27),
            "TEST",
            new Money(-100, "EUR"),
            TransactionCategory.EXPENSE);
        AssetTransaction assetTransaction = new AssetTransaction(transaction, "TEST", 10, AssetTransactionType.Buy);

        Assert.Throws<ArgumentException>(() => assetTransaction.ApplySplitFactor(0));
        Assert.Throws<ArgumentException>(() => assetTransaction.ApplySplitFactor(-0.5m));
        Assert.Equal(10, assetTransaction.Quantity);
    }

    [Fact]
    public void ApplySplitFactor_ShouldThrow_WhenLotWouldShrinkToZero()
    {
        Transaction transaction = new Transaction(
            new DateTime(2025, 8, 27),
            "TEST",
            new Money(-1, "EUR"),
            TransactionCategory.EXPENSE);
        AssetTransaction assetTransaction = new AssetTransaction(transaction, "TEST", 1, AssetTransactionType.Buy);

        Assert.Throws<ArgumentException>(() => assetTransaction.ApplySplitFactor(0.2m));
        Assert.Equal(1, assetTransaction.Quantity);
    }
}
