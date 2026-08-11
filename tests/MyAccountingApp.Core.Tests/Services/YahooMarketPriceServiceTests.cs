using System;
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
}
