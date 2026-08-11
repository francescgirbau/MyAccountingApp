using System.Text.Json;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.ValueObjects;

namespace MyAccountingApp.Domain.Tests.Entities;

public class TransactionTests
{
    [Fact]
    public void Constructor_ShouldCreateTransaction_WhenValidData()
    {
        // Arrange
        DateTime date = new DateTime(2025, 8, 27);
        Money money = new Money(amount: 100, currency: Currencies.EUR.ToString());
        string description = "Test Transaction";
        TransactionCategory category = TransactionCategory.INCOME;

        // Act
        Transaction transaction = new Transaction(date, description, money, category);

        // Assert
        Assert.Equal(date, transaction.Date);
        Assert.Equal(description, transaction.Description);
        Assert.Equal(money, transaction.Money);
        Assert.Equal(category, transaction.Category);
        Assert.NotEqual(Guid.Empty, transaction.Id);
    }

    [Theory]
    [InlineData(TransactionCategory.INCOME)]
    [InlineData(TransactionCategory.TRANSFER)]
    [InlineData(TransactionCategory.EXPENSE)]
    public void Constructor_ShouldThrow_WhenAmountIsZero(TransactionCategory category)
    {
        // Arrange
        DateTime date = DateTime.Now;
        Money money = new Money(amount: 0, currency: Currencies.EUR.ToString());

        // Act
        ArgumentException ex = Assert.Throws<ArgumentException>(() =>
            new Transaction(date, "Zero amount", money, category));

        // Assert
        Assert.Contains("cannot be zero", ex.Message);
    }

    [Fact]
    public void Constructor_ShouldAdjustSign_WhenCategoryIsIncome()
    {
        // Arrange
        DateTime date = DateTime.Now;
        Money money = new Money(amount: -50, currency: Currencies.EUR.ToString());

        // Act
        Transaction transaction = new Transaction(date, "Negative income", money, TransactionCategory.INCOME);

        // Assert - should adjust to positive too
        Assert.True(transaction.Money.Amount > 0);
    }

    [Fact]
    public void Constructor_ShouldAdjustSign_WhenCategoryIsExpense()
    {
        // Arrange
        DateTime date = DateTime.Now;
        Money money = new Money(amount: 50, currency: Currencies.EUR.ToString());

        // Act
        Transaction transaction = new Transaction(date, "Positive expense", money, TransactionCategory.EXPENSE);

        // Assert - should adjust to positve too
        Assert.True(transaction.Money.Amount > 0);
    }

    [Fact]
    public void UpdateCategory_ShouldChangeCategory()
    {
        // Arrange
        DateTime date = DateTime.Now;
        Money money = new Money(amount: 100, currency: Currencies.EUR.ToString());
        Transaction transaction = new Transaction(date, "Groceries", money, TransactionCategory.EXPENSE);

        // Act
        transaction.UpdateCategory(TransactionCategory.TRANSFER);

        // Assert
        Assert.Equal(TransactionCategory.TRANSFER, transaction.Category);
    }

    [Fact]
    public void Constructor_WithSource_SetsSource()
    {
        DateTime date = DateTime.Now;
        Money money = new Money(amount: 100, currency: Currencies.EUR.ToString());

        Transaction transaction = new Transaction(date, "Groceries", money, TransactionCategory.EXPENSE, "bank.csv");

        Assert.Equal("bank.csv", transaction.Source);
    }

    [Fact]
    public void Constructor_WithoutSource_DefaultsToNull()
    {
        DateTime date = DateTime.Now;
        Money money = new Money(amount: 100, currency: Currencies.EUR.ToString());

        Transaction transaction = new Transaction(date, "Groceries", money, TransactionCategory.EXPENSE);

        Assert.Null(transaction.Source);
    }

    [Fact]
    public void SetSource_ShouldUpdateSource()
    {
        DateTime date = DateTime.Now;
        Money money = new Money(amount: 100, currency: Currencies.EUR.ToString());
        Transaction transaction = new Transaction(date, "Groceries", money, TransactionCategory.EXPENSE);

        transaction.SetSource("ibkr.csv");

        Assert.Equal("ibkr.csv", transaction.Source);
    }

    [Fact]
    public void JsonConstructor_ShouldDeserializeWithoutSource_ForBackwardCompatibility()
    {
        string json = """
            {"id":"11111111-1111-1111-1111-111111111111","date":"2024-01-15T00:00:00","description":"Old tx","money":{"amount":100,"currency":"EUR"},"category":1}
            """;
        JsonSerializerOptions options = new() { PropertyNameCaseInsensitive = true };

        Transaction? transaction = JsonSerializer.Deserialize<Transaction>(json, options);

        Assert.NotNull(transaction);
        Assert.Null(transaction.Source);
        Assert.Equal(100m, transaction.Money.Amount);
    }

    [Fact]
    public void JsonConstructor_ShouldDeserializeWithSource()
    {
        string json = """
            {"id":"11111111-1111-1111-1111-111111111111","date":"2024-01-15T00:00:00","description":"Imported tx","money":{"amount":100,"currency":"EUR"},"category":1,"source":"bank.csv"}
            """;
        JsonSerializerOptions options = new() { PropertyNameCaseInsensitive = true };

        Transaction? transaction = JsonSerializer.Deserialize<Transaction>(json, options);

        Assert.NotNull(transaction);
        Assert.Equal("bank.csv", transaction.Source);
    }
}
