using System.Threading;
using MyAccountingApp.Domain.Interfaces;
using MyAccountingApp.Domain.ValueObjects;

namespace MyAccountingApp.Api.Tests.Fakes;

public class CountingMarketPriceService : IMarketPriceService
{
    private static int _calls;

    public static int Calls => Volatile.Read(ref _calls);

    public static void Reset() => Interlocked.Exchange(ref _calls, 0);

    public Task<Money?> GetPriceAsync(string symbol)
    {
        Interlocked.Increment(ref _calls);
        return Task.FromResult<Money?>(new Money(100m, "USD"));
    }

    public Task<Money?> RefreshPriceAsync(string symbol)
    {
        Interlocked.Increment(ref _calls);
        return Task.FromResult<Money?>(new Money(100m, "USD"));
    }

    public Task<Money?> GetCachedPriceAsync(string symbol) => Task.FromResult<Money?>(null);
}
