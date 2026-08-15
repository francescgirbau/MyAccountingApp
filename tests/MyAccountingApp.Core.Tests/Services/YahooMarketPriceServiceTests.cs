using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyAccountingApp.Core.Http.Market;
using MyAccountingApp.Domain.ValueObjects;
using Xunit;

namespace MyAccountingApp.Core.Tests.Services;

public class YahooMarketPriceServiceTests
{
    [Theory]
    [InlineData("AAPL", true)]
    [InlineData("BMW.DE", true)]
    [InlineData("VWRA.L", true)]
    [InlineData("COBAS_INTERNACIONAL_D", false)]
    [InlineData("SIGMA_INTERNACIONAL_A", false)]
    [InlineData("", false)]
    [InlineData("  ", false)]
    public void LooksLikeListedEquity_ReturnsExpectedResult(string symbol, bool expected)
    {
        Assert.Equal(expected, YahooMarketPriceService.LooksLikeListedEquity(symbol));
    }

    [Theory]
    [InlineData(null)]
    public void LooksLikeListedEquity_WithNull_ReturnsFalse(string? symbol)
    {
        Assert.False(YahooMarketPriceService.LooksLikeListedEquity(symbol));
    }

    [Fact]
    public void MarketPriceCache_ReturnsPrice_WithinTtl()
    {
        MarketPriceCache cache = new();
        Money price = new(150.25m, "USD");
        DateTimeOffset now = DateTimeOffset.UtcNow;

        cache.Set("AAPL", price, now);

        Assert.True(cache.TryGetFresh("AAPL", now.AddMinutes(29), out Money? cached));
        Assert.Equal(150.25m, cached.Amount);
        Assert.Equal("USD", cached.Currency);
    }

    [Fact]
    public void MarketPriceCache_ReturnsStale_AfterTtl()
    {
        MarketPriceCache cache = new();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        cache.Set("AAPL", new Money(150.25m, "USD"), now);

        Assert.False(cache.TryGetFresh("AAPL", now.AddMinutes(31), out _));
    }

    [Fact]
    public void MarketPriceCache_ReturnsStale_WhenNeverSet()
    {
        MarketPriceCache cache = new();

        Assert.False(cache.TryGetFresh("AAPL", DateTimeOffset.UtcNow, out _));
    }

    [Fact]
    public void MarketPriceCache_TryGetLast_ReturnsQuoteWithAsOfUtc_AfterTtl()
    {
        MarketPriceCache cache = new();
        DateTimeOffset fetchedAt = new DateTimeOffset(2025, 11, 21, 21, 0, 0, TimeSpan.Zero);

        cache.Set("AAPL", new Money(150.25m, "USD"), fetchedAt);

        Assert.False(cache.TryGetFresh("AAPL", fetchedAt.AddMinutes(31), out _));
        Assert.True(cache.TryGetLast("AAPL", out CachedQuote? last));
        Assert.Equal(150.25m, last.Price.Amount);
        Assert.Equal("USD", last.Price.Currency);
        Assert.Equal(fetchedAt, last.AsOfUtc);
    }

    [Fact]
    public void MarketPriceCache_TryGetLast_ReturnsFalse_WhenNeverSet()
    {
        MarketPriceCache cache = new();

        Assert.False(cache.TryGetLast("AAPL", out _));
    }

    [Fact]
    public async Task RefreshPriceAsync_RetainsLastQuote_WhenFetchReturnsNoQuote()
    {
        StubYahooMarketPriceService service = new();
        service.Handler = _ => Task.FromResult<Money?>(new Money(150.25m, "USD"));

        Money? first = await service.GetPriceAsync("AAPL");
        service.Handler = _ => Task.FromResult<Money?>(null);

        Money? refreshed = await service.RefreshPriceAsync("AAPL");

        Assert.Equal(150.25m, first?.Amount);
        Assert.Equal(150.25m, refreshed?.Amount);
    }

    [Fact]
    public async Task GetLastQuoteAsync_ReturnsCachedQuoteWithAsOfUtc()
    {
        StubYahooMarketPriceService service = new();
        service.Handler = _ => Task.FromResult<Money?>(new Money(150.25m, "USD"));

        await service.GetPriceAsync("AAPL");
        CachedQuote? last = await service.GetLastQuoteAsync("AAPL");
        CachedQuote? unknown = await service.GetLastQuoteAsync("UNKNOWN");

        Assert.NotNull(last);
        Assert.Equal(150.25m, last.Price.Amount);
        Assert.True((DateTimeOffset.UtcNow - last.AsOfUtc).TotalMinutes < 5);
        Assert.Null(unknown);
    }

    [Fact]
    public async Task GetPriceAsync_NormalizesHimax_BeforeProviderRequest()
    {
        StubYahooMarketPriceService service = new();
        service.Handler = _ => Task.FromResult<Money?>(new Money(1.23m, "USD"));

        Money? price = await service.GetPriceAsync("HIMAX");

        Assert.Equal("HIMX", Assert.Single(service.RequestedSymbols));
        Assert.Equal(1.23m, price?.Amount);
    }

    [Theory]
    [InlineData("HIMAX", "HIMX")]
    [InlineData("himax", "HIMX")]
    [InlineData("HIMX", "HIMX")]
    [InlineData("AAPL", "AAPL")]
    [InlineData(" AAPL ", "AAPL")]
    [InlineData(null, "")]
    public void NormalizeSymbol_ReturnsExpectedResult(string? symbol, string expected)
    {
        Assert.Equal(expected, YahooMarketPriceService.NormalizeSymbol(symbol));
    }

    [Theory]
    [InlineData("USD", "ccc_market", "USD")]
    [InlineData("usd", "us_market", "USD")]
    [InlineData(null, "us_market", "USD")]
    [InlineData("", "es_market", "EUR")]
    [InlineData("USD", "es_market", "USD")]
    public void ResolveCurrency_PrefersYahooCurrencyField(string? yahooCurrency, string market, string expected)
    {
        Assert.Equal(expected, YahooMarketPriceService.ResolveCurrency(yahooCurrency, market));
    }

    private sealed class StubYahooMarketPriceService : YahooMarketPriceService
    {
        public Func<string, Task<Money?>> Handler { get; set; } = _ => Task.FromResult<Money?>(null);

        public List<string> RequestedSymbols { get; } = new();

        protected override Task<Money?> FetchFromYahooAsync(string symbol)
        {
            this.RequestedSymbols.Add(symbol);
            return this.Handler(symbol);
        }
    }
}
