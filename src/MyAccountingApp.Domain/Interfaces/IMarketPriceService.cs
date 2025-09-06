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
}
