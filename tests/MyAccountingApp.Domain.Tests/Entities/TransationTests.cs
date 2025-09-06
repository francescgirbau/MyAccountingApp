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
    public void Constructor_ShouldThrow_WhenAmountNegativeAndCategoryIncome()
    {
        // Arrange
        DateTime date = DateTime.Now;
        Money money = new Money(amount: -50, currency: Currencies.EUR.ToString());

        // Act
        ArgumentException ex = Assert.Throws<ArgumentException>(() =>
            new Transaction(date, "Negative income", money, TransactionCategory.INCOME));

        // Assert
        Assert.Contains("cannot be negative", ex.Message);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenAmountPositiveAndCategoryExpense()
    {
        // Arrange
        DateTime date = DateTime.Now;
        Money money = new Money(amount: 50, currency: Currencies.EUR.ToString());

        // Act
        ArgumentException ex = Assert.Throws<ArgumentException>(() =>
            new Transaction(date, "Positive expense", money, TransactionCategory.EXPENSE));

        // Assert
        Assert.Contains("cannot be positive", ex.Message);
    }
}
