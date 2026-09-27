using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.ValueObjects;

namespace MyAccountingApp.Domain.Entities;
public class AssetTransaction
{
    public Transaction Transaction { get; private set; }
    public string Symbol { get; private set; }
    public decimal Quantity { get; private set; }
    public AssetTransactionType Type { get; private set; }

    /// <summary>
    /// Gets the provenance of the asset transaction (e.g. the imported file name), or null.
    /// </summary>
    public string? Source { get; private set; }

    public AssetTransaction(
        Transaction transaction,
        string symbol,
        decimal quantity,
        AssetTransactionType type)
    {
        Transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
        Symbol = symbol;
        Quantity = quantity < 0 ? -quantity : quantity;
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

    public void UpdateSymbol(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new ArgumentException("Symbol cannot be null or empty.");
        }

        this.Symbol = symbol;
    }

    /// <summary>
    /// Applies a split (factor &gt; 1) or reverse split / consolidation (factor &lt; 1) to this lot,
    /// keeping the total invested amount unchanged. The unitary cost is derived from
    /// Amount / Quantity, so it adjusts automatically (e.g. a 5:1 consolidation turns
    /// 1000 shares @ 0.09 into 200 shares @ 0.45 with the same 90.00 total).
    /// </summary>
    public void ApplySplitFactor(decimal factor)
    {
        if (factor <= 0)
        {
            throw new ArgumentException("Split factor must be greater than zero.");
        }

        decimal newQuantity = Math.Round(this.Quantity * factor, 0, MidpointRounding.AwayFromZero);
        if (newQuantity <= 0)
        {
            throw new ArgumentException("Split factor leaves no shares on this lot.");
        }

        this.Quantity = newQuantity;
    }

    public void SetSource(string? source)
    {
        this.Source = source;
    }

    public Money UnitaryCost()
    {
        if (Quantity == 0)
        {
            return new Money(0, this.Transaction.Money.Currency);
        }

        decimal unitaryAmount = (Transaction.Money.Amount < 0 ? -Transaction.Money.Amount : Transaction.Money.Amount) / Quantity;

        return new Money(unitaryAmount, Transaction.Money.Currency);
    }
}
