using MyAccountingApp.Core.Persistence;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.ValueObjects;

namespace MyAccountingApp.Core.Tests.Repositories;

public class JsonMarketQuoteRepositoryTests : IDisposable
{
    private readonly string _tempFile;

    public JsonMarketQuoteRepositoryTests()
    {
        this._tempFile = Path.GetTempFileName();
    }

    public void Dispose()
    {
        if (File.Exists(this._tempFile))
        {
            File.Delete(this._tempFile);
        }
    }

    [Fact]
    public void Upsert_ShouldPersistQuote_AndNormalizeSymbol()
    {
        JsonMarketQuoteRepository repo = new(this._tempFile);
        MarketQuote quote = new("aapl", new DateOnly(2026, 9, 27), new Money(150.25m, "USD"), "Yahoo", DateTimeOffset.UtcNow);

        repo.Upsert(quote);

        MarketQuote? loaded = repo.GetLatest("AAPL");
        Assert.NotNull(loaded);
        Assert.Equal("AAPL", loaded!.Symbol);
        Assert.Equal(150.25m, loaded.Price.Amount);
        Assert.Equal("USD", loaded.Price.Currency);
        Assert.Equal("Yahoo", loaded.Provider);
    }

    [Fact]
    public void Upsert_SameSymbolSameDay_ShouldReplaceQuote()
    {
        JsonMarketQuoteRepository repo = new(this._tempFile);
        repo.Upsert(new MarketQuote("AAPL", new DateOnly(2026, 9, 27), new Money(10m, "USD"), "Yahoo", DateTimeOffset.UtcNow));
        repo.Upsert(new MarketQuote("AAPL", new DateOnly(2026, 9, 27), new Money(11m, "USD"), "Yahoo", DateTimeOffset.UtcNow));

        Assert.Single(repo.GetAll());
        Assert.Equal(11m, repo.GetLatest("AAPL")?.Price.Amount);
    }

    [Fact]
    public void Upsert_DifferentDays_ShouldKeepBoth()
    {
        JsonMarketQuoteRepository repo = new(this._tempFile);
        repo.Upsert(new MarketQuote("AAPL", new DateOnly(2026, 9, 26), new Money(10m, "USD"), "Yahoo", DateTimeOffset.UtcNow));
        repo.Upsert(new MarketQuote("AAPL", new DateOnly(2026, 9, 27), new Money(11m, "USD"), "Yahoo", DateTimeOffset.UtcNow));

        Assert.Equal(2, repo.GetAll().Count);
    }

    [Fact]
    public void GetLatest_ShouldReturnNewestDay_ForSymbol()
    {
        JsonMarketQuoteRepository repo = new(this._tempFile);
        repo.Upsert(new MarketQuote("AAPL", new DateOnly(2026, 9, 26), new Money(10m, "USD"), "Yahoo", DateTimeOffset.UtcNow));
        repo.Upsert(new MarketQuote("AAPL", new DateOnly(2026, 9, 27), new Money(11m, "USD"), "Yahoo", DateTimeOffset.UtcNow));
        repo.Upsert(new MarketQuote("MSFT", new DateOnly(2026, 9, 27), new Money(300m, "USD"), "Yahoo", DateTimeOffset.UtcNow));

        MarketQuote? latest = repo.GetLatest("AAPL");

        Assert.NotNull(latest);
        Assert.Equal(new DateOnly(2026, 9, 27), latest!.Date);
        Assert.Equal(11m, latest.Price.Amount);
    }

    [Fact]
    public void GetLatest_ShouldReturnNull_WhenNoQuoteExists()
    {
        JsonMarketQuoteRepository repo = new(this._tempFile);

        Assert.Null(repo.GetLatest("AAPL"));
    }

    [Fact]
    public void GetAll_ShouldBeEmpty_WhenFileDoesNotExist()
    {
        JsonMarketQuoteRepository repo = new(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json"));

        Assert.Empty(repo.GetAll());
    }
}