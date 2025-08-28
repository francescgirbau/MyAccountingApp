using System.Text.Json.Serialization;
using MyAccountingApp.Core.Enums;
using MyAccountingApp.Core.ValueObjects;

namespace MyAccountingApp.Core.Entities;

/// <summary>
/// Represents a financial transaction, including its date, description, amount, and category.
/// </summary>
public class Transaction
{
    /// <summary>
    /// Gets the unique identifier for the transaction.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Gets the date of the transaction.
    /// </summary>
    public DateTime Date { get; private set; }

    /// <summary>
    /// Gets the description of the transaction.
    /// </summary>
    public string Description { get; private set; }

    /// <summary>
    /// Gets the monetary value of the transaction.
    /// </summary>
    public Money Money { get; private set; }

    /// <summary>
    /// Gets the category of the transaction (expense, income, or transfer).
    /// </summary>
    public TransactionCategory Category { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Transaction"/> class.
    /// </summary>
    /// <param name="date">The date of the transaction.</param>
    /// <param name="description">The description of the transaction.</param>
    /// <param name="money">The monetary value of the transaction.</param>
    /// <param name="category">The category of the transaction.</param>
    /// <exception cref="ArgumentException">
    /// Thrown if the amount is zero, or if the amount does not match the expected sign for the category.
    /// </exception>
    public Transaction(DateTime date, string description, Money money, TransactionCategory category)
    {
        this.Id = Guid.NewGuid();
        this.Date = date;
        this.Description = description;
        this.Money = money;
        this.Category = category;

        this.Validate();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Transaction"/> class.
    /// </summary>
    /// <param name="id">The id of the transaction.</param>
    /// <param name="date">The date of the transaction.</param>
    /// <param name="description">The description of the transaction.</param>
    /// <param name="money">The monetary value of the transaction.</param>
    /// <param name="category">The category of the transaction.</param>
    [JsonConstructor]
    public Transaction(Guid id, DateTime date, string description, Money money, TransactionCategory category)
    {
        this.Id = id;
        this.Date = date;
        this.Description = description;
        this.Money = money;
        this.Category = category;
    }

    /// <summary>
    /// Validates the transaction data.
    /// Ensures the amount is not zero and matches the expected sign for the category.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Thrown if the amount is zero, or if the amount does not match the expected sign for the category.
    /// </exception>
    private void Validate()
    {
        string parentType = nameof(Transaction);

        if (this.Money.Amount == 0)
        {
            string message = $"The {nameof(this.Money.Amount)} cannot be zero, you provided {this.Money.Amount} {this.Money.Currency}";

            throw new ArgumentException(message, parentType);
        }

        if (this.Money.Amount > 0 && this.Category == TransactionCategory.EXPENSE)
        {
            string message = $"The {nameof(this.Money.Amount)} cannot be positive, when the transaction is an {nameof(TransactionCategory.EXPENSE)}, you provided {this.Money.Amount} {this.Money.Currency}";

            throw new ArgumentException(message, parentType);
        }

        if (this.Money.Amount < 0 && this.Category == TransactionCategory.INCOME)
        {
            string message = $"The {nameof(this.Money.Amount)} cannot be negative, when the transaction is an {nameof(TransactionCategory.INCOME)}, you provided {this.Money.Amount} {this.Money.Currency}";

            throw new ArgumentException(message, parentType);
        }
    }
}
