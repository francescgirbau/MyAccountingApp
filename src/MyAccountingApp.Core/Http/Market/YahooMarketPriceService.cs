using MyAccountingApp.Domain.Interfaces;
using MyAccountingApp.Domain.ValueObjects;
using YahooFinanceApi;

namespace MyAccountingApp.Core.Http.Market;
public class YahooMarketPriceService : IMarketPriceService
{
    // Yahoo's quote API rejects request bursts (HTTP 401). Outgoing requests are throttled
    // process-wide: at most 2 concurrent, with at least 600 ms between request starts, so a
    // 40-symbol refresh becomes a paced queue instead of a parallel burst. This protects
    // every caller (refresh-prices, /api/portfolio, validation) without changing their code.
    private static readonly SemaphoreSlim ThrottleConcurrency = new(2, 2);
    private static readonly SemaphoreSlim ThrottlePacer = new(1, 1);
    private const long MinRequestIntervalMs = 600;
    private static long _lastRequestStartMs;

    private readonly MarketPriceCache _cache = new();

    public Task<Money?> GetPriceAsync(string symbol, string? quoteCurrency = null) => this.FetchAsync(symbol, useCache: true, quoteCurrency);

    public Task<Money?> RefreshPriceAsync(string symbol, string? quoteCurrency = null) => this.FetchAsync(symbol, useCache: false, quoteCurrency);

    public Task<Money?> GetCachedPriceAsync(string symbol)
    {
        string normalized = NormalizeSymbol(symbol);

        if (!LooksLikeListedEquity(normalized))
        {
            return Task.FromResult<Money?>(null);
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        Money? cached = this._cache.TryGetFresh(normalized, now, out Money? price) ? price : null;
        return Task.FromResult(cached);
    }

    public Task<CachedQuote?> GetLastQuoteAsync(string symbol)
    {
        string normalized = NormalizeSymbol(symbol);

        if (!LooksLikeListedEquity(normalized))
        {
            return Task.FromResult<CachedQuote?>(null);
        }

        return Task.FromResult(this._cache.TryGetLast(normalized, out CachedQuote? quote) ? quote : null);
    }

    private async Task<Money?> FetchAsync(string symbol, bool useCache, string? quoteCurrency = null)
    {
        string normalized = NormalizeSymbol(symbol);

        if (!LooksLikeListedEquity(normalized))
        {
            return null;
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;

        if (useCache && this._cache.TryGetFresh(normalized, now, out Money? cachedPrice))
        {
            return cachedPrice;
        }

        Money? price = await this.FetchFromYahooAsync(normalized, quoteCurrency);

        if (price is not null)
        {
            this._cache.Set(normalized, price, now);
        }
        else if (this._cache.TryGetLast(normalized, out CachedQuote? last))
        {
            price = last.Price;
        }

        return price;
    }

    /// <summary>
    /// Normalizes a ticker before it hits the provider or the cache (e.g. HIMAX -&gt; HIMX).
    /// </summary>
    public static string NormalizeSymbol(string? symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return string.Empty;
        }

        string trimmed = symbol.Trim();
        return trimmed.Equals("HIMAX", StringComparison.OrdinalIgnoreCase) ? "HIMX" : trimmed;
    }

    /// <summary>
    /// Heuristic to detect fund symbols (e.g. COBAS_*, SIGMA_*) that are not listed on Yahoo.
    /// </summary>
    public static bool LooksLikeListedEquity(string? symbol) =>
        !string.IsNullOrWhiteSpace(symbol) && !symbol.Contains('_');

    protected virtual async Task<Money?> FetchFromYahooAsync(string symbol, string? quoteCurrency = null)
    {
        try
        {
            Money? quote = await this.FetchQuoteAsync(symbol);
            if (quote is not null)
            {
                return quote;
            }

            // Some bare tickers have no quote on Yahoo while the exchange-suffixed ticker does
            // (e.g. CEQ -> CEQ.V, YAL -> YAL.AX). Retry with the suffix that matches the
            // position's known quote currency before giving up.
            foreach (string candidate in GetSuffixCandidates(symbol, quoteCurrency))
            {
                quote = await this.FetchQuoteAsync(candidate);
                if (quote is not null)
                {
                    return quote;
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching price for {symbol}: {ex.Message}");
            return null;
        }
    }

    protected virtual async Task<Money?> FetchQuoteAsync(string symbol)
    {
        IReadOnlyDictionary<string, Security> securities = await ThrottledYahooCallAsync(
            () => Yahoo.Symbols(symbol).Fields(Field.Symbol, Field.RegularMarketPrice, Field.Currency).QueryAsync());

        if (!securities.TryGetValue(symbol, out Security? security))
        {
            return null;
        }

        // Yahoo sometimes returns ticker metadata without a live quote (no price and no
        // currency field). Treat those as "no quote" instead of throwing KeyNotFoundException.
        if (!security.Fields.ContainsKey("RegularMarketPrice"))
        {
            return null;
        }

        bool hasCurrency = security.Fields.ContainsKey("Currency");
        bool hasMarket = security.Fields.ContainsKey("Market");
        if (!hasCurrency && !hasMarket)
        {
            return null;
        }

        decimal amount = (decimal)security.RegularMarketPrice;
        string currency = ResolveCurrency(hasCurrency ? security.Currency : null, hasMarket ? security.Market : null);
        return new Money(amount, currency);
    }

    /// <summary>
    /// Runs a Yahoo HTTP call under the process-wide throttle: at most 2 requests in flight at
    /// once, and each request start is spaced at least <see cref="MinRequestIntervalMs"/> ms
    /// apart. Keeps bursts short enough for Yahoo to accept them (it answers bursts with 401).
    /// </summary>
    private static async Task<T> ThrottledYahooCallAsync<T>(Func<Task<T>> action)
    {
        await ThrottleConcurrency.WaitAsync();
        try
        {
            await ThrottlePacer.WaitAsync();
            try
            {
                long now = Environment.TickCount64;
                long remaining = (Interlocked.Read(ref _lastRequestStartMs) + MinRequestIntervalMs) - now;
                if (remaining > 0)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(remaining));
                }

                Interlocked.Exchange(ref _lastRequestStartMs, Environment.TickCount64);
            }
            finally
            {
                ThrottlePacer.Release();
            }

            return await action();
        }
        finally
        {
            ThrottleConcurrency.Release();
        }
    }

    /// <summary>
    /// Returns the Yahoo exchange-suffixed tickers to try for a symbol that has no bare quote,
    /// derived from the position's known quote currency (e.g. CEQ + CAD -&gt; CEQ.TO, CEQ.V).
    /// </summary>
    public static IReadOnlyList<string> GetSuffixCandidates(string symbol, string? quoteCurrency)
    {
        if (string.IsNullOrWhiteSpace(symbol) || string.IsNullOrWhiteSpace(quoteCurrency))
        {
            return Array.Empty<string>();
        }

        return quoteCurrency.Trim().ToUpperInvariant() switch
        {
            "CAD" => new[] { symbol + ".TO", symbol + ".V" },
            "AUD" => new[] { symbol + ".AX" },
            "GBP" => new[] { symbol + ".L" },
            "EUR" => new[] { symbol + ".MC" },
            "CHF" => new[] { symbol + ".SW" },
            "SEK" => new[] { symbol + ".ST" },
            "NOK" => new[] { symbol + ".OL" },
            "HKD" => new[] { symbol + ".HK" },
            "JPY" => new[] { symbol + ".T" },
            "DKK" => new[] { symbol + ".CO" },
            _ => Array.Empty<string>(),
        };
    }

    /// <summary>
    /// Resolves the quote currency: prefers Yahoo's explicit currency field (crypto pairs like
    /// ADA-USD report it), falling back to the market-based mapping for listed equities.
    /// </summary>
    public static string ResolveCurrency(string? yahooCurrency, string? market)
    {
        if (!string.IsNullOrWhiteSpace(yahooCurrency))
        {
            return yahooCurrency.Trim().ToUpperInvariant();
        }

        return MapYahooMarketIntoCurrency(market ?? string.Empty);
    }

    private static string MapYahooMarketIntoCurrency(string market)
    {
        market = market?.ToLowerInvariant() ?? string.Empty;

        return market switch
        {
            "us_market" => "USD", // United States
            "es_market" => "EUR", // Spain
            "gb_market" => "GBP", // United Kingdom
            "ca_market" => "CAD", // Canada
            "au_market" => "AUD", // Australia
            "ch_market" => "CHF", // Switzerland
            "hk_market" => "HKD", // Hong Kong
            "no_market" => "NOK", // Norway
            "br_market" => "BRL", // Brazil
            "ar_market" => "ARS", // Argentina
            "cn_market" => "CNY", // China
            "jp_market" => "JPY", // Japan
            "se_market" => "SEK", // Sweden
            "mx_market" => "MXN", // Mexico
            "in_market" => "INR", // India
            "ru_market" => "RUB", // Russia
            "sg_market" => "SGD", // Singapore
            "tr_market" => "TRY", // Turkey
            _ => throw new NotSupportedException($"Unknown market '{market}' for currency mapping")
        };
    }
}
