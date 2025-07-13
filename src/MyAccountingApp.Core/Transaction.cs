using MyAccountingApp.Shared;

namespace MyAccountingApp.Core;


public class Transaction
{
    public Guid Id { get; }
    public DateTime Date { get; }
    public string Description { get; }
    public Money Money { get; } 
    public TransactionCategory Category { get; }


    public Transaction(DateTime date, string description, Money money, TransactionCategory category)
    {
        Id = new Guid();
        Date = date;
        Description = description;
        Money = money;
        Category = category;

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
