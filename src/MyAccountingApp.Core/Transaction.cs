namespace MyAccountingApp.Core;

public class Transaction
{
    public Guid Id { get; }
    public DateTime Date { get; }
    public string Description { get; }
    public Money Money { get; }
    public TransactionCategory Category { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Transaction"/> class.
    /// </summary>
    /// <param name="date">The date of the transaction.</param>
    /// <param name="description">A description of the transaction.</param>
    /// <param name="money">The monetary value and currency of the transaction.</param>
    /// <param name="category">The category of the transaction (Expense, Income, Transfer).</param>
    public Transaction(DateTime date, string description, Money money, TransactionCategory category)
    {
        this.Id = new Guid();
        this.Date = date;
        this.Description = description;
        this.Money = money;
        this.Category = category;

        Validate();
    }

    private void Validate()
    {
        string parentType = nameof(Transaction);

        if (this.Money.Amount == 0)
        {
            string message = $"The {nameof(Money.Amount)} cannot be zero, you provided {this.Money.Amount} {this.Money.Currency}";

            throw new ArgumentException(message, parentType);

        }

        if (this.Money.Amount < 0 && this.Category == TransactionCategory.EXPENSE)
        {
            string message = $"The {nameof(Money.Amount)} cannot be negative, when the transaction is an Expense, you provided {this.Money.Amount} {this.Money.Currency}";

            throw new ArgumentException(message, parentType);
        }

        if (this.Money.Amount > 0 && this.Category == TransactionCategory.INCOME)
        {
            string message = $"The {nameof(Money.Amount)} cannot be negative, when the transaction is an Expense, you provided {this.Money.Amount} {this.Money.Currency}";

            throw new ArgumentException(message, parentType);
        }
    }
}
