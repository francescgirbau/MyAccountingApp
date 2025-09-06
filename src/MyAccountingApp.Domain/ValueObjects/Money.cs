namespace MyAccountingApp.Domain.ValueObjects;

/// <summary>
/// Represents a monetary value with an amount and a currency.
/// </summary>
public record Money
{
    /// <summary>
    /// Gets the amount of money.
    /// </summary>
    public double Amount { get; }

    /// <summary>
    /// Gets the currency of the money.
    /// </summary>
    public string Currency { get; }

    /// <summary>
    /// The constructor for the Money value object.
    /// </summary>
    /// <param name="amount">The amount of money</param>
    /// <param name="currency">The currency code</param>
    /// <exception cref="ArgumentException"></exception>
    public Money(double amount, string currency)
    {
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
        {
            throw new ArgumentException("Currency code must be a valid ISO 4217 code.", nameof(currency));
        }

        this.Amount = amount;
        this.Currency = currency.ToUpperInvariant();
    }
}
