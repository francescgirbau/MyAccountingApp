namespace MyAccountingApp.Domain.Entities;

using System.Text.Json.Serialization;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.ValueObjects;

public class OptionTransaction
{
    public Transaction Transaction { get; }

    public string Symbol { get; private set; }

    public string Isin { get; }

    public decimal Quantity { get; }

    public AssetTransactionType Type { get; }

    public OptionTransaction(
        Transaction transaction,
        string symbol,
        string isin,
        decimal quantity,
        AssetTransactionType type)
    {
        Transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
        Symbol = symbol;
        Isin = isin;
        Quantity = quantity < 0 ? -quantity : quantity;
        Type = type;

        Validate();
    }

    private void Validate()
    {
        if (string.IsNullOrWhiteSpace(Symbol))
        {
            throw new ArgumentException("Symbol cannot be null or empty.");
        }

        if (Quantity <= 0)
        {
            throw new ArgumentException("Quantity must be greater than zero.");
        }
    }

    public void UpdateSymbol(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new ArgumentException("Symbol cannot be null or empty.");
        }

        this.Symbol = symbol;
    }
}
