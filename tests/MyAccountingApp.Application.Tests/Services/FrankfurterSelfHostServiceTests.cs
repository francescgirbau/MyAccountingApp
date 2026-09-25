using MyAccountingApp.Application.Services;
using MyAccountingApp.Core.DTOs;
using MyAccountingApp.Core.Persistence;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Exceptions;

namespace MyAccountingApp.Application.Tests.Services;

public class FrankfurterSelfHostServiceTests
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
    public void GetRates_ForEurBase_ReturnsRecordsForCachedDay()
    {
        // Arrange
        InMemoryConversionRepository repo = new();
        repo.AddOrUpdate(EurConversion(new DateTime(2026, 8, 1), (Currencies.USD, 1.1m), (Currencies.CAD, 1.5m)));
        FrankfurterSelfHostService service = new(repo);

        // Act
        IReadOnlyList<FrankfurterRateRecord> records = service.GetRatesAsync(new DateOnly(2026, 8, 1), Currencies.EUR);

        // Assert
        Assert.Equal(2, records.Count);
        Assert.All(records, r => Assert.Equal("2026-08-01", r.Date));
        Assert.All(records, r => Assert.Equal("EUR", r.Base));
        Assert.Equal(1.5m, Assert.Single(records, r => r.Quote == "CAD").Rate);
        Assert.Equal(1.1m, Assert.Single(records, r => r.Quote == "USD").Rate);
    }

    [Fact]
    public void GetRates_ForNonEurBase_DerivesCrossRates()
    {
        // Arrange
        InMemoryConversionRepository repo = new();
        repo.AddOrUpdate(EurConversion(new DateTime(2026, 8, 1), (Currencies.USD, 1.1m), (Currencies.CAD, 1.5m)));
        FrankfurterSelfHostService service = new(repo);

        // Act
        IReadOnlyList<FrankfurterRateRecord> records = service.GetRatesAsync(new DateOnly(2026, 8, 1), Currencies.USD);

        // Assert
        FrankfurterRateRecord cad = Assert.Single(records);
        Assert.Equal("USD", cad.Base);
        Assert.Equal("CAD", cad.Quote);
        Assert.Equal(1.5m / 1.1m, cad.Rate);
    }

    [Fact]
    public void GetRates_WithQuotesFilter_ReturnsOnlyRequestedQuotes()
    {
        // Arrange
        InMemoryConversionRepository repo = new();
        repo.AddOrUpdate(EurConversion(new DateTime(2026, 8, 1), (Currencies.USD, 1.1m), (Currencies.CAD, 1.5m)));
        FrankfurterSelfHostService service = new(repo);

        // Act
        IReadOnlyList<FrankfurterRateRecord> records = service.GetRatesAsync(new DateOnly(2026, 8, 1), Currencies.EUR, new[] { Currencies.USD });

        // Assert
        FrankfurterRateRecord usd = Assert.Single(records);
        Assert.Equal("USD", usd.Quote);
        Assert.Equal(1.1m, usd.Rate);
    }

    [Fact]
    public void GetRates_ForMissingDate_ThrowsConversionNotAvailable()
    {
        // Arrange
        FrankfurterSelfHostService service = new(new InMemoryConversionRepository());

        // Act & Assert
        Assert.Throws<ConversionNotAvailableException>(() => service.GetRatesAsync(new DateOnly(2026, 8, 1), Currencies.EUR));
    }

    [Fact]
    public void GetRatesRange_ReturnsRecordsForAllCachedDays()
    {
        // Arrange
        InMemoryConversionRepository repo = new();
        repo.AddOrUpdate(EurConversion(new DateTime(2026, 8, 1), (Currencies.USD, 1.1m), (Currencies.CAD, 1.5m)));
        repo.AddOrUpdate(EurConversion(new DateTime(2026, 8, 2), (Currencies.USD, 1.12m), (Currencies.CAD, 1.52m)));
        FrankfurterSelfHostService service = new(repo);

        // Act
        IReadOnlyList<FrankfurterRateRecord> records = service.GetRatesAsync(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 5), Currencies.EUR);

        // Assert
        Assert.Equal(4, records.Count);
        Assert.Equal(2, records.Count(r => r.Date == "2026-08-01"));
        Assert.Equal(2, records.Count(r => r.Date == "2026-08-02"));
        Assert.Equal(1.12m, Assert.Single(records, r => r.Date == "2026-08-02" && r.Quote == "USD").Rate);
    }

    [Fact]
    public void GetRatesRange_WithNoCachedDays_ThrowsConversionNotAvailable()
    {
        // Arrange
        FrankfurterSelfHostService service = new(new InMemoryConversionRepository());

        // Act & Assert
        Assert.Throws<ConversionNotAvailableException>(() => service.GetRatesAsync(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31), Currencies.EUR));
    }
}