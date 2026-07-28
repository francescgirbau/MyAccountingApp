namespace MyAccountingApp.Domain.Entities;

using System.Text.Json.Serialization;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.ValueObjects;

public class OptionTransaction
{
    public Guid Id { get; private set; }

    public DateTime Date { get; private set; }

    public string Description { get; private set; }

    public string Symbol { get; private set; }

    public string Isin { get; private set; }

    public decimal Quantity { get; private set; }

    public Money Premium { get; private set; }

    public AssetTransactionType Type { get; private set; }

    public OptionTransaction(
        DateTime date,
        string description,
        string symbol,
        string isin,
        decimal quantity,
        Money premium,
        AssetTransactionType type)
    {
        Id = Guid.NewGuid();
        Date = date;
        Description = description;
        Symbol = symbol;
        Isin = isin;
        Quantity = quantity < 0 ? -quantity : quantity;
        Premium = premium;
        Type = type;

        Validate();
    }

    [JsonConstructor]
    public OptionTransaction(
        Guid id,
        DateTime date,
        string description,
        string symbol,
        string isin,
        decimal quantity,
        Money premium,
        AssetTransactionType type)
    {
        Id = id;
        Date = date;
        Description = description;
        Symbol = symbol;
        Isin = isin;
        Quantity = quantity;
        Premium = premium;
        Type = type;
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

        if (Premium.Amount <= 0)
        {
            throw new ArgumentException("Premium amount must be greater than zero.");
        }
    }
}
