using MyAccountingApp.Domain.ValueObjects;

namespace MyAccountingApp.Domain.Entities;

/// <summary>
/// A market quote observed for a symbol on a given day. Persisted quotes let the app reuse the
/// price fetched the same day (instead of hitting the provider again) and keep a per-day audit
/// trail of the prices that were actually used.
/// </summary>
public class MarketQuote
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MarketQuote"/> class.
    /// </summary>
    /// <param name="symbol">The normalized symbol/ticker the quote belongs to.</param>
    /// <param name="date">The calendar day the quote is stored for (UTC).</param>
    /// <param name="price">The quoted price.</param>
    /// <param name="provider">The provider the quote came from (e.g. Yahoo).</param>
    /// <param name="retrievedAtUtc">The instant the quote was retrieved from the provider.</param>
    public MarketQuote(string symbol, DateOnly date, Money price, string? provider, DateTimeOffset retrievedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new ArgumentException("Symbol cannot be null or empty.", nameof(symbol));
        }

        this.Symbol = symbol.Trim().ToUpperInvariant();
        this.Date = date;
        this.Price = price;
        this.Provider = provider;
        this.RetrievedAtUtc = retrievedAtUtc;
    }

    /// <summary>
    /// Gets the symbol/ticker this quote belongs to (uppercase).
    /// </summary>
    public string Symbol { get; }

    /// <summary>
    /// Gets the calendar day this quote is stored for (UTC).
    /// </summary>
    public DateOnly Date { get; }

    /// <summary>
    /// Gets the quoted price.
    /// </summary>
    public Money Price { get; }

    /// <summary>
    /// Gets the provider the quote came from (e.g. Yahoo), when known.
    /// </summary>
    public string? Provider { get; }

    /// <summary>
    /// Gets the instant the quote was retrieved from the provider.
    /// </summary>
    public DateTimeOffset RetrievedAtUtc { get; }
}