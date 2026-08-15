using MyAccountingApp.Domain.ValueObjects;

namespace MyAccountingApp.Domain.Interfaces;
public interface IMarketPriceService
{
    /// <summary>
    /// Return el price of asset based on its ticker
    /// </summary>
    /// <param name="symbol">Ticker</param>
    /// <returns>Price</returns>
    Task<Money?> GetPriceAsync(string symbol);

    /// <summary>
    /// Return el price of asset based on its ticker, bypassing the in-memory cache
    /// </summary>
    /// <param name="symbol">Ticker</param>
    /// <returns>Price</returns>
    Task<Money?> RefreshPriceAsync(string symbol);

    /// <summary>
    /// Return el price of asset from the in-memory cache only, without fetching
    /// </summary>
    /// <param name="symbol">Ticker</param>
    /// <returns>Cached price, or null if not cached</returns>
    Task<Money?> GetCachedPriceAsync(string symbol);

    /// <summary>
    /// Return el last valid cached quote (even if stale) with its AsOfUtc, without fetching
    /// </summary>
    /// <param name="symbol">Ticker</param>
    /// <returns>Last valid cached quote, or null if the symbol never had a price</returns>
    Task<CachedQuote?> GetLastQuoteAsync(string symbol);
}
