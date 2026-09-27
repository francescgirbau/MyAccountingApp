using MyAccountingApp.Domain.Entities;

namespace MyAccountingApp.Domain.Interfaces;

/// <summary>
/// Repository abstraction for persisted market quotes (at most one per symbol per day).
/// </summary>
public interface IMarketQuoteRepository
{
    /// <summary>
    /// Gets the most recent quote stored for a symbol, or null when the symbol never had a quote.
    /// </summary>
    /// <param name="symbol">The symbol/ticker to look up.</param>
    /// <returns>The latest stored quote, or null.</returns>
    MarketQuote? GetLatest(string symbol);

    /// <summary>
    /// Stores a quote, replacing any existing quote for the same symbol on the same day.
    /// </summary>
    /// <param name="quote">The quote to store.</param>
    void Upsert(MarketQuote quote);

    /// <summary>
    /// Gets all stored quotes.
    /// </summary>
    /// <returns>All stored quotes.</returns>
    IReadOnlyList<MarketQuote> GetAll();
}