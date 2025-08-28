using System.Net;
using MyAccountingApp.Core.Enums;
using MyAccountingApp.Infrastructure.Services;
using MyAccountingApp.TestUtilities.Fakes;

namespace MyAccountingApp.Infrastructure.Tests.Services;
public class CurrencyConverterTests
{
    [Fact]
    public async Task FetchAllRatesAsync_ReturnsRates_WhenApiResponseIsValid()
    {
        // Arrange
        Dictionary<string, double> expectedQuotes = new Dictionary<string, double> { { "EURUSD", 1.1 }, { "EURGBP", 0.85 } };
        string responseContent = @"{""success"":true,""quotes"":{""EURUSD"":1.1,""EURGBP"":0.85}}";
        HttpClient httpClient = FakeHttpClient.CreateFakeHttpClient(responseContent, HttpStatusCode.OK);

        CurrencyConverter converter = new CurrencyConverter(httpClient);

        // Act
        Dictionary<string, double> result = await converter.FetchAllRatesAsync(Currencies.EUR, new DateTime(2024, 1, 1));

        // Assert
        Assert.Equal(expectedQuotes, result);
    }

    [Fact]
    public async Task FetchAllRatesAsync_ThrowsException_WhenApiResponseIsInvalid()
    {
        // Arrange
        string responseContent = @"{""success"":false,""error"":{""info"":""Invalid API key""}}";
        HttpClient httpClient = FakeHttpClient.CreateFakeHttpClient(responseContent, HttpStatusCode.OK);

        CurrencyConverter converter = new CurrencyConverter(httpClient);

        // Act
        Exception ex = await Assert.ThrowsAsync<Exception>(() =>
            converter.FetchAllRatesAsync(Currencies.USD, new DateTime(2024, 1, 1)));

        // Assert
        Assert.Contains("Invalid API key", ex.Message);
    }

    [Fact]
    public async Task FetchAllRatesAsync_ThrowsException_WhenHttpStatusIsNotSuccess()
    {
        // Arrange
        HttpClient httpClient = FakeHttpClient.CreateFakeHttpClient("{}", HttpStatusCode.BadRequest);
        CurrencyConverter converter = new CurrencyConverter(httpClient);

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            converter.FetchAllRatesAsync(Currencies.CAD, new DateTime(2024, 1, 1)));
    }
}
