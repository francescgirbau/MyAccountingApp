using MyAccountingApp.Application.Services;
using MyAccountingApp.Core.Enums;
using MyAccountingApp.Tests.Fakes;

namespace MyAccountingApp.Tests;

public class CurrencyConversionServiceTests
{
    [Fact]
    public void TransactionWithZeroAmountIsNotValid()
    {
        // Arrange
        Currencies invalidSource = Currencies.USD;

        DateTime date = new DateTime(2023, 12, 1); // This date does not exist

        FakeConversionRepository fakeRepo = new(); // repositori in-memory per testing
        FakeCurrencyConverter fakeApi = new();     // fake API que retorna quotes

        // Act
        Action action = () => { new CurrencyRateService(fakeRepo, fakeApi, invalidSource); };

        // Assert
        Assert.Throws<ArgumentException>(() => action());
    }

    [Fact]
    public async Task GetExchangeRateAsync_AddsConversion_WhenMissing()
    {
        // Arrange
        FakeConversionRepository fakeRepo = new FakeConversionRepository(); // repositori in-memory per testing
        FakeCurrencyConverter fakeApi = new FakeCurrencyConverter();     // fake API que retorna quotes
        Currencies source = Currencies.EUR;

        CurrencyRateService service = new(fakeRepo, fakeApi, source);

        DateTime date = new DateTime(2023, 12, 1); // This date does not exist
        Currencies targetCurrency = Currencies.USD;
        double expectedTargetRate = 1.1;

        // Act
        Dictionary<Currencies, double> rate = await service.GetQuotes(date);

        // Assert
        Assert.Equal(rate[targetCurrency], expectedTargetRate);
        Assert.True(fakeRepo.CalledAdd);
        Assert.True(fakeRepo.ExistsForDate(date));
    }

    [Fact]
    public async Task GetExchangeRateAsync_WhenIsNotMissing()
    {
        // Arrange
        FakeConversionRepository fakeRepo = new(); // repositori in-memory per testing
        FakeCurrencyConverter fakeApi = new();     // fake API que retorna quotes
        Currencies source = Currencies.EUR;

        CurrencyRateService service = new CurrencyRateService(fakeRepo, fakeApi, source);

        DateTime date = new DateTime(2005, 12, 1); // This date does  exist
        Currencies targetCurrency = Currencies.USD;
        double expectedTargetRate = 1.1;

        // Act
        Dictionary<Currencies, double> rate = await service.GetQuotes(date);

        // Assert
        Assert.Equal(rate[targetCurrency], expectedTargetRate);
        Assert.False(fakeRepo.CalledAdd);
        Assert.True(fakeRepo.ExistsForDate(date));
    }
}
