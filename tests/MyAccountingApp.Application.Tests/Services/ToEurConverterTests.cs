using MyAccountingApp.Application.DTOs;
using MyAccountingApp.Application.Services;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Exceptions;
using MyAccountingApp.Domain.Interfaces;
using MyAccountingApp.Domain.ValueObjects;
using MyAccountingApp.TestUtilities.Fakes;

namespace MyAccountingApp.Application.Tests.Services;

public class ToEurConverterTests
{
    private static ToEurConverter CreateService(FakeApiQuotaManager quota)
    {
        FakeConversionRepository repo = new();
        FakePendingConversionQueue queue = new();
        CurrencyRateService rateService = new(repo, new FakeCurrencyConverter(), Currencies.EUR, quota, queue);
        return new ToEurConverter(rateService);
    }

    [Fact]
    public async Task ToEurAsync_ReturnsUnchanged_WhenCurrencyIsEur()
    {
        ToEurConverter converter = CreateService(new FakeApiQuotaManager());
        Money money = new(123.45m, "EUR");

        EurConversionDto result = await converter.ToEurAsync(money, new DateOnly(2026, 8, 11));

        Assert.Equal(123.45m, result.AmountEur);
        Assert.Equal(1m, result.Rate);
        Assert.Equal(new DateOnly(2026, 8, 11), result.RateDate);
        Assert.False(result.IsStale);
        Assert.Equal("base", result.Provider);
    }

    [Fact]
    public async Task ToEurAsync_DividesByRate_WhenNonEur()
    {
        ToEurConverter converter = CreateService(new FakeApiQuotaManager());
        Money money = new(110m, "USD");

        EurConversionDto result = await converter.ToEurAsync(money, new DateOnly(2023, 12, 1));

        Assert.Equal(100m, result.AmountEur);
        Assert.Equal(1.1m, result.Rate);
        Assert.Equal(new DateOnly(2023, 12, 1), result.RateDate);
        Assert.False(result.IsStale);
    }

    [Fact]
    public async Task ToEurAsync_ExposesFallbackRateDate_WhenStale()
    {
        ToEurConverter converter = CreateService(new FakeApiQuotaManager() { CanConsumeResult = false });
        Money money = new(110m, "USD");

        EurConversionDto result = await converter.ToEurAsync(money, new DateOnly(2023, 12, 1));

        Assert.Equal(100m, result.AmountEur);
        Assert.Equal(1.1m, result.Rate);
        Assert.Equal(new DateOnly(2005, 12, 1), result.RateDate);
        Assert.True(result.IsStale);
    }

    [Fact]
    public async Task ToEurAsync_Throws_WhenNoQuoteForCurrency()
    {
        ToEurConverter converter = CreateService(new FakeApiQuotaManager());
        Money money = new(100m, "JPY");

        await Assert.ThrowsAsync<ConversionNotAvailableException>(() => converter.ToEurAsync(money, new DateOnly(2023, 12, 1)));
    }
}