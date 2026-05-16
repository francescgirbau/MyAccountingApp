using FsCheck.Xunit;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.ValueObjects;

namespace MyAccountingApp.Core.Tests.Properties;

public class UnitaryCostPropertyTests
{
    [Property]
    public bool UnitaryCost_MultipliedByQuantity_EqualsTransactionAmount(
        decimal amount, decimal quantity, string currencyCode)
    {
        currencyCode = currencyCode is { Length: 3 } ? currencyCode.ToUpperInvariant() : "EUR";
        amount = Math.Abs(amount);
        if (amount == 0) { amount = 100; }

        quantity = Math.Abs(quantity);
        if (quantity == 0) { quantity = 10; }

        Money money = new Money(amount, currencyCode);
        Transaction transaction = new Transaction(
            new DateTime(2024, 1, 1), "Test", money, TransactionCategory.EXPENSE);
        AssetTransaction assetTx = new AssetTransaction(
            transaction, "TEST", quantity, AssetTransactionType.Buy);

        decimal unitaryCost = assetTx.UnitaryCost().Amount;
        decimal totalFromCost = unitaryCost * quantity;

        return Math.Abs(totalFromCost - amount) < 0.01m;
    }

    [Property]
    public bool UnitaryCost_IsAlwaysPositive(decimal amount, decimal quantity)
    {
        amount = Math.Abs(amount);
        if (amount == 0) { amount = 100; }

        quantity = Math.Abs(quantity);
        if (quantity == 0) { quantity = 10; }

        Money money = new Money(amount, "EUR");
        Transaction transaction = new Transaction(
            new DateTime(2024, 1, 1), "Test", money, TransactionCategory.EXPENSE);
        AssetTransaction assetTx = new AssetTransaction(
            transaction, "TEST", quantity, AssetTransactionType.Buy);

        return assetTx.UnitaryCost().Amount > 0;
    }

    [Property]
    public bool UnitaryCost_WithQuantityOne_EqualsAmount(decimal amount)
    {
        amount = Math.Abs(amount);
        if (amount == 0) { amount = 100; }

        Money money = new Money(amount, "EUR");
        Transaction transaction = new Transaction(
            new DateTime(2024, 1, 1), "Test", money, TransactionCategory.EXPENSE);
        AssetTransaction assetTx = new AssetTransaction(
            transaction, "TEST", 1, AssetTransactionType.Buy);

        decimal roundedAmount = Math.Round(amount, 4);
        return Math.Abs(assetTx.UnitaryCost().Amount - roundedAmount) < 0.0001m;
    }
}
