using MyAccountingApp.Application.Interfaces;
using MyAccountingApp.Application.Services;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.ValueObjects;

namespace MyAccountingApp.Application.Tests.Services;

public class TransactionValidatorTests
{
    private readonly TransactionValidator _validator = new();

    [Fact]
    public void ValidateTransaction_ValidTransaction_ReturnsValid()
    {
        Transaction tx = new(
            Guid.NewGuid(),
            new DateTime(2024, 6, 1),
            "Salary",
            new Money(3000, "EUR"),
            TransactionCategory.INCOME);

        ValidationResult result = this._validator.Validate(tx);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void ValidateTransaction_FutureDate_ReturnsError()
    {
        Transaction tx = new(
            Guid.NewGuid(),
            DateTime.UtcNow.AddDays(2),
            "Future",
            new Money(100, "EUR"),
            TransactionCategory.INCOME);

        ValidationResult result = this._validator.Validate(tx);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "Date" && e.Severity == "error");
    }

    [Fact]
    public void ValidateTransaction_OldDate_ReturnsWarning()
    {
        Transaction tx = new(
            Guid.NewGuid(),
            new DateTime(1999, 1, 1),
            "Old",
            new Money(100, "EUR"),
            TransactionCategory.INCOME);

        ValidationResult result = this._validator.Validate(tx);

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, e => e.Field == "Date" && e.Severity == "warning");
    }

    [Fact]
    public void ValidateTransaction_EmptyDescription_ReturnsError()
    {
        Transaction tx = new(
            Guid.NewGuid(),
            new DateTime(2024, 6, 1),
            "",
            new Money(100, "EUR"),
            TransactionCategory.INCOME);

        ValidationResult result = this._validator.Validate(tx);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "Description");
    }

    [Fact]
    public void ValidateTransaction_InvalidCurrency_ReturnsError()
    {
        Transaction tx = new(
            Guid.NewGuid(),
            new DateTime(2024, 6, 1),
            "Test",
            new Money(100, ""),
            TransactionCategory.INCOME);

        ValidationResult result = this._validator.Validate(tx);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "Currency");
    }

    [Fact]
    public void ValidateAssetTransaction_ValidTransaction_ReturnsValid()
    {
        Transaction tx = new(
            Guid.NewGuid(),
            new DateTime(2024, 6, 1),
            "Buy AAPL",
            new Money(1500, "USD"),
            TransactionCategory.EXPENSE);
        AssetTransaction assetTx = new(tx, "AAPL", 10, AssetTransactionType.Buy);

        ValidationResult result = this._validator.Validate(assetTx);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateAssetTransaction_EmptySymbol_ReturnsError()
    {
        Transaction tx = new(
            Guid.NewGuid(),
            new DateTime(2024, 6, 1),
            "Buy",
            new Money(1500, "USD"),
            TransactionCategory.EXPENSE);
        AssetTransaction assetTx = new(tx, "", 10, AssetTransactionType.Buy);

        ValidationResult result = this._validator.Validate(assetTx);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "Symbol");
    }
}
