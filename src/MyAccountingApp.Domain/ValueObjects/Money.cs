using MyAccountingApp.Domain.Enums;

namespace MyAccountingApp.Domain.ValueObjects;

/// <summary>
/// Represents a monetary value with an amount and a currency.
/// </summary>
public record Money
{
    /// <summary>
    /// Gets or initializes the amount of money.
    /// </summary>
    public double Amount { get; init; }

    /// <summary>
    /// Gets or initializes the currency of the money.
    /// </summary>
    public Currencies Currency { get; init; }
}
