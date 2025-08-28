using MyAccountingApp.Application.Services;
using MyAccountingApp.Core.Enums;
using MyAccountingApp.Core.Interfaces;
using MyAccountingApp.TestUtilities.Fakes;

namespace MyAccountingApp.Application.Tests.Services;

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
        Action action = () => { new CurencyRateService(fakeRepo, fakeApi, invalidSource); };

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

        CurencyRateService service = new(fakeRepo, fakeApi, source);

        DateTime date = new DateTime(2023, 12, 1); // This date does not exist
        Currencies targetCurrency = Currencies.USD;
        double expectedTargetRate = 1.1;

        // Act
        Dictionary<Currencies, double> rate = await service.GetQuotes(date);

        // Assert
        Assert.Equal(rate[targetCurrency], expectedTargetRate);
        Assert.True(fakeRepo.CalledAdd);
        Assert.NotNull(fakeRepo.GetByDate(date));
    }

    [Fact]
    public async Task GetExchangeRateAsync_WhenIsNotMissing()
    {
        // Arrange
        FakeConversionRepository fakeRepo = new(); // repositori in-memory per testing
        FakeCurrencyConverter fakeApi = new();     // fake API que retorna quotes
        Currencies source = Currencies.EUR;

        CurencyRateService service = new CurencyRateService(fakeRepo, fakeApi, source);

        DateTime date = new DateTime(2005, 12, 1); // This date does  exist
        Currencies targetCurrency = Currencies.USD;
        double expectedTargetRate = 1.1;

        // Act
        Dictionary<Currencies, double> rate = await service.GetQuotes(date);

        // Assert
        Assert.Equal(rate[targetCurrency], expectedTargetRate);
        Assert.False(fakeRepo.CalledAdd);
        Assert.NotNull(fakeRepo.GetByDate(date));
    }

    [Fact]
    public async Task ConvertToAsync_ShouldConvert_USD_To_EUR_OnGivenDate()
    {
        // Arrange
        ICurrencyConverter converter = new FakeCurrencyConverter();
        Currencies source = Currencies.EUR;
        (string, double) expectedRateUsd = ("EURUSD", 1.1);
        (string, double) expectedRateCad = ("EURCAD", 1.5);

        DateTime date = new DateTime(2023, 12, 1);

        // Act
        Dictionary<string, double> result = await converter.FetchAllRatesAsync(source, date);

        // Assert
        Assert.True(result.ContainsKey(expectedRateUsd.Item1));
        Assert.Equal(result[expectedRateUsd.Item1], expectedRateUsd.Item2);
        Assert.True(result.ContainsKey(expectedRateCad.Item1));
        Assert.Equal(result[expectedRateCad.Item1], expectedRateCad.Item2);
    }
}
