using MyAccountingApp.Domain.ValueObjects;

namespace MyAccountingApp.Core.Http.Market;

public class MarketPriceCache
{
    public static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(30);

    private readonly TimeSpan _ttl;
    private readonly Dictionary<string, CacheEntry> _entries = new();
    private readonly object _lock = new();

    public MarketPriceCache(TimeSpan? ttl = null)
    {
        this._ttl = ttl ?? DefaultTtl;
    }

    public bool TryGetFresh(string symbol, DateTimeOffset now, out Money price)
    {
        lock (this._lock)
        {
            if (this._entries.TryGetValue(symbol, out CacheEntry? entry) && now - entry.FetchedAt < this._ttl)
            {
                price = entry.Price;
                return true;
            }
        }

        price = default!;
        return false;
    }

    public void Set(string symbol, Money price, DateTimeOffset now)
    {
        lock (this._lock)
        {
            this._entries[symbol] = new CacheEntry(price, now);
        }
    }

    private sealed record CacheEntry(Money Price, DateTimeOffset FetchedAt);
}
