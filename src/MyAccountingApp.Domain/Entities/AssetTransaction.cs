using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.ValueObjects;

namespace MyAccountingApp.Domain.Entities;
public class AssetTransaction
{
    public Transaction Transaction { get; private set; }
    public string Symbol { get; private set; }
    public double Quantity { get; private set; }
    public AssetTransactionType Type { get; private set; }

    public AssetTransaction(Transaction transaction, string symbol, double quantity, AssetTransactionType type)
    {
        Transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
        Symbol = symbol;
        Quantity = Math.Abs(quantity);
        Type = type;

        this.Validate();
    }

    private void Validate()
    {
        if (string.IsNullOrWhiteSpace(this.Symbol))
        {
            throw new ArgumentException("Symbol cannot be null or empty.");
        }

        if (this.Quantity <= 0)
        {
            throw new ArgumentException("Quantity must be greater than zero.");
        }
    }

    public Money UnitaryCost()
    {
        if (Quantity == 0)
        {
            return new Money(0, this.Transaction.Money.Currency);
        }

        double unitaryAmount = Math.Abs(Transaction.Money.Amount) / Quantity;

        return new Money(unitaryAmount, Transaction.Money.Currency);
    }
}
