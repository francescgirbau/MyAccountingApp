using MyAccountingApp.Application.Services;
using MyAccountingApp.Core.Enums;
using MyAccountingApp.Tests.Fakes;


namespace MyAccountingApp.Tests;

public class CurrencyConversionServiceTests
{
    private const double PRECISION = 0.001;

    [Fact]
    public async Task GetExchangeRateAsync_AddsConversion_WhenMissing()
    {
        // Arrange
        var fakeRepo = new FakeConversionRepository(); // repositori in-memory per testing
        var fakeApi = new FakeCurrencyConverter();     // fake API que retorna quotes

        var service = new CurrencyConversionService(fakeRepo, fakeApi);

        var date = new DateTime(2023, 12, 1); // This date does not exist
        var targetCurrency = Currencies.USD;

        // Act
        var rate = await service.GetExchangeRateAsync(targetCurrency, date);

        // Assert
        Assert.True(1.1 - rate < PRECISION);
        Assert.True(fakeRepo.CalledAdd);
        Assert.True(fakeRepo.ExistsForDate(date));
    }

    [Fact]
    public async Task GetExchangeRateAsync_WhenIsNotMissing()
    {
        // Arrange
        var fakeRepo = new FakeConversionRepository(); // repositori in-memory per testing
        var fakeApi = new FakeCurrencyConverter();     // fake API que retorna quotes

        var service = new CurrencyConversionService(fakeRepo, fakeApi);

        var date = new DateTime(2005, 12, 1); // This date does  exist
        var targetCurrency = Currencies.USD;

        // Act
        var rate = await service.GetExchangeRateAsync(targetCurrency, date);

        // Assert
        Assert.True(1.1 - rate < PRECISION);
        Assert.False(fakeRepo.CalledAdd);
        Assert.True(fakeRepo.ExistsForDate(date));
    }
}
