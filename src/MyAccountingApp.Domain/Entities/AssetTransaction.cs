using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.ValueObjects;

namespace MyAccountingApp.Domain.Entities;
public class AssetTransaction
{
    /// <summary>
    /// The associated financial transaction.
    /// </summary>
    public Transaction Transaction { get; private set; }
    /// <summary>
    /// The asset symbol (e.g., stock ticker).
    /// </summary>
    public string Symbol { get; private set; }
    /// <summary>
    /// The quantity of the asset involved in the transaction.
    /// </summary>
    public double Quantity { get; private set; }
    /// <summary>
    /// The type of asset transaction (buy, sell, dividend, etc.).
    /// </summary>
    public AssetTransactionType Type { get; private set; }

    public AssetTransaction(Transaction transaction, string symbol, double quantity, AssetTransactionType type)
    {
        Transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
        Symbol = symbol;
        Quantity = quantity;
        Type = type;

        this.Validate();
    }

    private void Validate()
    {
        string parentType = nameof(AssetTransaction);
        if (string.IsNullOrWhiteSpace(this.Symbol))
        {
            string message = $"The {nameof(this.Symbol)} cannot be null or empty.";
            throw new ArgumentException(message, parentType);
        }

        if (this.Type == AssetTransactionType.Buy && this.Transaction.Category != TransactionCategory.EXPENSE)
        {
            string message = $"The {nameof(this.Transaction.Category)} must be {TransactionCategory.EXPENSE} for {nameof(AssetTransactionType.Buy)}, you provided {this.Transaction.Category}.";
            throw new ArgumentException(message, parentType);
        }

        if ((this.Type == AssetTransactionType.Sell || this.Type == AssetTransactionType.Dividend) && this.Transaction.Category != TransactionCategory.INCOME)
        {
            string message = $"The {nameof(this.Transaction.Category)} must be {TransactionCategory.INCOME} for {nameof(AssetTransactionType.Dividend)} or {nameof(AssetTransactionType.Sell)}, you provided {this.Transaction.Category}.";
            throw new ArgumentException(message, parentType);
        }

        if (this.Quantity <= 0 && this.Type != AssetTransactionType.Dividend)
        {
            string message = $"The {nameof(this.Quantity)} must be greater than zero for non {nameof(AssetTransactionType.Dividend)}, you provided {this.Quantity}.";
            throw new ArgumentException(message, parentType);
        }

        if (this.Quantity != 0 && this.Type == AssetTransactionType.Dividend)
        {
            string message = $"The {nameof(this.Quantity)} must zero for {nameof(AssetTransactionType.Dividend)}, you provided {this.Quantity}.";
            throw new ArgumentException(message, parentType);
        }
    }

    public Money UnitaryCost()
    {
        if (Quantity == 0)
        {
            return new Money(0, this.Transaction.Money.Currency);
        }

        double unitaryAmount = Transaction.Money.Amount / Quantity;

        return new Money(unitaryAmount, Transaction.Money.Currency);

    }
}
