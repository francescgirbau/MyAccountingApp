using MyAccountingApp.Application.Services;
using MyAccountingApp.Core.Persistence;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Exceptions;
using MyAccountingApp.TestUtilities.Fakes;

namespace MyAccountingApp.Application.Tests.Services;

public class CachedCurrencyConverterTests
{
    private static Conversion EurConversion(DateTime date, params (Currencies Currency, decimal Rate)[] quotes)
    {
        Conversion conversion = new(date, Currencies.EUR, sourceProvider: "frankfurter");

        foreach ((Currencies currency, decimal rate) in quotes)
        {
            conversion.AddOrUpdateQuote(currency, rate);
        }

        return conversion;
    }

    [Fact]
    public async Task FetchAllRates_CachedDate_ReturnsPairRates()
    {
        // Arrange
        InMemoryConversionRepository repo = new();
        repo.AddOrUpdate(EurConversion(new DateTime(2026, 8, 1), (Currencies.USD, 1.1m), (Currencies.CAD, 1.5m)));
        CachedCurrencyConverter converter = new(repo);

        // Act
        Dictionary<string, decimal> rates = await converter.FetchAllRatesAsync(Currencies.EUR, new DateTime(2026, 8, 1));

        // Assert
        Assert.Equal(2, rates.Count);
        Assert.Equal(1.1m, rates["EURUSD"]);
        Assert.Equal(1.5m, rates["EURCAD"]);
    }

    [Fact]
    public async Task FetchAllRates_NonEurSource_DerivesCrossPairs()
    {
        // Arrange
        InMemoryConversionRepository repo = new();
        repo.AddOrUpdate(EurConversion(new DateTime(2026, 8, 1), (Currencies.USD, 1.1m), (Currencies.CAD, 1.5m)));
        CachedCurrencyConverter converter = new(repo);

        // Act
        Dictionary<string, decimal> rates = await converter.FetchAllRatesAsync(Currencies.USD, new DateTime(2026, 8, 1));

        // Assert
        Assert.Equal(1.5m / 1.1m, rates["USDCAD"]);
        Assert.False(rates.ContainsKey("USDUSD"));
    }

    [Fact]
    public async Task FetchAllRates_MissingDate_WithoutFallback_Throws()
    {
        // Arrange
        CachedCurrencyConverter converter = new(new InMemoryConversionRepository());

        // Act & Assert
        await Assert.ThrowsAsync<ConversionNotAvailableException>(() => converter.FetchAllRatesAsync(Currencies.EUR, new DateTime(2026, 8, 1)));
    }

    [Fact]
    public async Task FetchAllRates_MissingDate_WithFallback_Delegates()
    {
        // Arrange
        FakeCurrencyConverter fallback = new();
        CachedCurrencyConverter converter = new(new InMemoryConversionRepository(), fallback);

        // Act
        Dictionary<string, decimal> rates = await converter.FetchAllRatesAsync(Currencies.EUR, new DateTime(2026, 8, 1));

        // Assert
        Assert.Equal(1, fallback.FetchAllCalls);
        Assert.Equal(2, rates.Count);
        Assert.Equal(1.1m, rates["EURUSD"]);
    }

    [Fact]
    public async Task FetchAllRates_MissingDate_WithFallback_ServesFromCacheWhenPresent()
    {
        // Arrange
        InMemoryConversionRepository repo = new();
        repo.AddOrUpdate(EurConversion(new DateTime(2026, 8, 1), (Currencies.USD, 1.1m), (Currencies.CAD, 1.5m)));
        FakeCurrencyConverter fallback = new();
        CachedCurrencyConverter converter = new(repo, fallback);

        // Act
        Dictionary<string, decimal> rates = await converter.FetchAllRatesAsync(Currencies.EUR, new DateTime(2026, 8, 1));

        // Assert
        Assert.Equal(0, fallback.FetchAllCalls);
        Assert.Equal(1.5m, rates["EURCAD"]);
    }

    [Fact]
    public async Task FetchRange_CachedDays_ReturnsOnlyCachedDays()
    {
        // Arrange
        InMemoryConversionRepository repo = new();
        repo.AddOrUpdate(EurConversion(new DateTime(2026, 8, 1), (Currencies.USD, 1.1m)));
        CachedCurrencyConverter converter = new(repo);

        // Act
        IReadOnlyDictionary<DateOnly, Dictionary<string, decimal>> result = await converter.FetchRangeAsync(Currencies.EUR, new DateOnly(2026, 7, 30), new DateOnly(2026, 8, 2));

        // Assert
        Assert.Single(result);
        Assert.True(result.ContainsKey(new DateOnly(2026, 8, 1)));
    }

    [Fact]
    public async Task FetchRange_MissingDays_WithFallback_MergesFallbackDays()
    {
        // Arrange
        FakeCurrencyConverter fallback = new();
        CachedCurrencyConverter converter = new(new InMemoryConversionRepository(), fallback);

        // Act
        IReadOnlyDictionary<DateOnly, Dictionary<string, decimal>> result = await converter.FetchRangeAsync(Currencies.EUR, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 3));

        // Assert
        Assert.Equal(1, fallback.FetchRangeCalls);
        Assert.Equal(3, result.Count);
        Assert.True(result.ContainsKey(new DateOnly(2026, 8, 2)));
    }
}