using MyAccountingApp.Application.DTOs;
using MyAccountingApp.Application.Services;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Exceptions;
using MyAccountingApp.Domain.ValueObjects;
using MyAccountingApp.TestUtilities.Fakes;

namespace MyAccountingApp.Application.Tests.Services;

public class ToEurConverterTests
{
    [Fact]
    public async Task ToEurAsync_ConvertsUsingRate_ForRequestedDate()
    {
        ToEurConverter converter = CreateService(new FakeApiQuotaManager());

        EurConversionDto result = await converter.ToEurAsync(new Money(110m, "USD"), new DateOnly(2023, 12, 1));

        Assert.Equal(100m, result.AmountEur);
        Assert.Equal(1.1m, result.Rate);
        Assert.Equal(new DateOnly(2023, 12, 1), result.RateDate);
        Assert.False(result.IsStale);
    }

    [Fact]
    public async Task ToEurAsync_ExposesFallbackRateDate_WhenStale()
    {
        FakeConversionRepository repo = new();
        repo.Initialize(new[] { new Conversion(new DateTime(2023, 11, 29), Currencies.EUR, new Dictionary<Currencies, decimal> { { Currencies.USD, 1.1m } }) });
        FakeApiQuotaManager quota = new() { CanConsumeResult = false };
        ToEurConverter converter = new(new CurrencyRateService(repo, new FakeCurrencyConverter(), Currencies.EUR, quota, new FakePendingConversionQueue()));

        EurConversionDto result = await converter.ToEurAsync(new Money(110m, "USD"), new DateOnly(2023, 12, 1));

        Assert.Equal(100m, result.AmountEur);
        Assert.Equal(1.1m, result.Rate);
        Assert.Equal(new DateOnly(2023, 11, 29), result.RateDate);
        Assert.True(result.IsStale);
    }

    [Fact]
    public async Task ToEurAsync_Throws_WhenNoQuoteWithinFiveDays()
    {
        FakeConversionRepository repo = new();
        repo.Initialize(new[] { new Conversion(new DateTime(2023, 11, 20), Currencies.EUR, new Dictionary<Currencies, decimal> { { Currencies.USD, 1.1m } }) });
        FakeApiQuotaManager quota = new() { CanConsumeResult = false };
        ToEurConverter converter = new(new CurrencyRateService(repo, new FakeCurrencyConverter(), Currencies.EUR, quota, new FakePendingConversionQueue()));

        await Assert.ThrowsAsync<ConversionNotAvailableException>(() => converter.ToEurAsync(new Money(110m, "USD"), new DateOnly(2023, 12, 1)));
    }

    [Fact]
    public async Task ToEurAsync_Throws_WhenNoQuoteForCurrency()
    {
        ToEurConverter converter = CreateService(new FakeApiQuotaManager());

        await Assert.ThrowsAsync<ConversionNotAvailableException>(() => converter.ToEurAsync(new Money(100m, "JPY"), new DateOnly(2023, 12, 1)));
    }

    private static ToEurConverter CreateService(FakeApiQuotaManager quota)
    {
        FakeConversionRepository repo = new();
        FakePendingConversionQueue queue = new();
        CurrencyRateService rateService = new(repo, new FakeCurrencyConverter(), Currencies.EUR, quota, queue);
        return new ToEurConverter(rateService);
    }
}