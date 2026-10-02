using System.Threading;
using MyAccountingApp.Domain.Interfaces;
using MyAccountingApp.Domain.ValueObjects;

namespace MyAccountingApp.Api.Tests.Fakes;

public class CountingMarketPriceService : IMarketPriceService
{
    private int _calls;

    public int Calls => Volatile.Read(ref this._calls);

    public void Reset() => Interlocked.Exchange(ref this._calls, 0);

    public Task<Money?> GetPriceAsync(string symbol, string? quoteCurrency = null)
    {
        Interlocked.Increment(ref this._calls);
        return Task.FromResult<Money?>(new Money(100m, "USD"));
    }

    public Task<Money?> RefreshPriceAsync(string symbol, string? quoteCurrency = null)
    {
        Interlocked.Increment(ref this._calls);
        return Task.FromResult<Money?>(new Money(100m, "USD"));
    }

    public Task<Money?> GetCachedPriceAsync(string symbol) => Task.FromResult<Money?>(null);

    public Task<CachedQuote?> GetLastQuoteAsync(string symbol) => Task.FromResult<CachedQuote?>(null);
}