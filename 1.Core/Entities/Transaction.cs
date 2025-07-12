using _2.Shared.Models;

namespace _1.Core.Entities
{
    public class Transaction
    {
        public Guid Id { get; }
        public DateTime Date { get; }
        public string Description { get; }
        public Money Amount { get; } 
        public TransactionCategory Category { get; }


        public Transaction(DateTime date, string description, Money amount, TransactionCategory category)
        {
            Id = new Guid();
            Date = date;
            Description = description;
            Amount = amount;
            Category = category;

            Validate();

        }

        private void Validate()
        {
            string parentType = nameof(Transaction);

            if (this.Amount.Value == 0)
            {
                string message = $"The {nameof(Money.Value)} cannot be zero, you provided {this.Amount.Value} {this.Amount.Currency}";

                throw new ArgumentException(message, parentType);

            }
            
            if (this.Amount.Value < 0 && this.Category == TransactionCategory.EXPENSE)
            {
                string message = $"The {nameof(Money.Value)} cannot be negative, when the transaction is an Expense, you provided {this.Amount.Value} {this.Amount.Currency}";

                throw new ArgumentException(message, parentType);
            }

            if (this.Amount.Value > 0 && this.Category == TransactionCategory.INCOME)
            {
                string message = $"The {nameof(Money.Value)} cannot be negative, when the transaction is an Expense, you provided {this.Amount.Value} {this.Amount.Currency}";

                throw new ArgumentException(message, parentType);
            }
        }



       

    }
}
