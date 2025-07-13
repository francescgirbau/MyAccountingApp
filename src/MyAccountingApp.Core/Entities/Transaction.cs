using MyAccountingApp.Core.Enums;
using MyAccountingApp.Core.ValueObjects;

namespace MyAccountingApp.Core.Entities;
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

        if (Money.Amount == 0)
        {
            string message = $"The {nameof(Money.Amount)} cannot be zero, you provided {Money.Amount} {Money.Currency}";

            throw new ArgumentException(message, parentType);

        }
            
        if (Money.Amount < 0 && Category == TransactionCategory.EXPENSE)
        {
            string message = $"The {nameof(Money.Amount)} cannot be negative, when the transaction is an Expense, you provided {Money.Amount} {Money.Currency}";

            throw new ArgumentException(message, parentType);
        }

        if (Money.Amount > 0 && Category == TransactionCategory.INCOME)
        {
            string message = $"The {nameof(Money.Amount)} cannot be negative, when the transaction is an Expense, you provided {Money.Amount} {Money.Currency}";

            throw new ArgumentException(message, parentType);
        }
    }



       

}
