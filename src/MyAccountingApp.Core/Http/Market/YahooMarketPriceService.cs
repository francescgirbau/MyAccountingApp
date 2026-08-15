using MyAccountingApp.Domain.Interfaces;
using MyAccountingApp.Domain.ValueObjects;
using YahooFinanceApi;

namespace MyAccountingApp.Core.Http.Market;
public class YahooMarketPriceService : IMarketPriceService
{
    private readonly MarketPriceCache _cache = new();

    public Task<Money?> GetPriceAsync(string symbol) => this.FetchAsync(symbol, useCache: true);

    public Task<Money?> RefreshPriceAsync(string symbol) => this.FetchAsync(symbol, useCache: false);

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

    private async Task<Money?> FetchAsync(string symbol, bool useCache)
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

        Money? price = await this.FetchFromYahooAsync(normalized);

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

    protected virtual async Task<Money?> FetchFromYahooAsync(string symbol)
    {
        try
        {
            IReadOnlyDictionary<string, Security> securities = await Yahoo.Symbols(symbol).Fields(Field.Symbol, Field.RegularMarketPrice, Field.Currency).QueryAsync();

            if (securities.TryGetValue(symbol, out Security? security))
            {
                string currency = ResolveCurrency(security.Currency, security.Market);
                decimal amount = (decimal)security.RegularMarketPrice;

                return new Money(amount, currency);
            }

            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching price for {symbol}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Resolves the quote currency: prefers Yahoo's explicit currency field (crypto pairs like
    /// ADA-USD report it), falling back to the market-based mapping for listed equities.
    /// </summary>
    public static string ResolveCurrency(string? yahooCurrency, string market)
    {
        if (!string.IsNullOrWhiteSpace(yahooCurrency))
        {
            return yahooCurrency.Trim().ToUpperInvariant();
        }

        return MapYahooMarketIntoCurrency(market);
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
