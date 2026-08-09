using System.Net;
using MyAccountingApp.Core.Http.Currency;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Exceptions;
using MyAccountingApp.TestUtilities.Fakes;

namespace MyAccountingApp.Core.Tests.Services;

public class FrankfurterCurrencyConverterTests
{
    [Fact]
    public async Task FetchAllRatesAsync_ReturnsRates_WhenApiResponseIsValid()
    {
        // Arrange
        string responseContent = """[{"date":"2025-01-02","base":"EUR","quote":"USD","rate":1.07},{"date":"2025-01-02","base":"EUR","quote":"GBP","rate":0.85}]""";
        HttpClient httpClient = FakeHttpClient.CreateFakeHttpClient(responseContent, HttpStatusCode.OK);
        FrankfurterCurrencyConverter converter = new(httpClient);

        // Act
        Dictionary<string, decimal> result = await converter.FetchAllRatesAsync(Currencies.EUR, new DateTime(2025, 1, 2));

        // Assert
        Assert.Equal(1.07m, result["EURUSD"]);
        Assert.Equal(0.85m, result["EURGBP"]);
    }

    [Fact]
    public async Task FetchAllRatesAsync_ThrowsException_WhenApiReturnsErrorMessage()
    {
        // Arrange
        string responseContent = """{"message":"Could not find currency ABC"}""";
        HttpClient httpClient = FakeHttpClient.CreateFakeHttpClient(responseContent, HttpStatusCode.NotFound);
        FrankfurterCurrencyConverter converter = new(httpClient);

        // Act
        Exception ex = await Assert.ThrowsAsync<Exception>(() => converter.FetchAllRatesAsync(Currencies.EUR, new DateTime(2025, 1, 2)));

        // Assert
        Assert.Contains("Could not find currency ABC", ex.Message);
    }

    [Fact]
    public async Task FetchAllRatesAsync_ThrowsQuotaExceeded_WhenHttp429()
    {
        // Arrange
        HttpClient httpClient = FakeHttpClient.CreateFakeHttpClient("{}", HttpStatusCode.TooManyRequests);
        FrankfurterCurrencyConverter converter = new(httpClient);

        // Act & Assert
        await Assert.ThrowsAsync<CurrencyApiQuotaExceededException>(() => converter.FetchAllRatesAsync(Currencies.EUR, new DateTime(2025, 1, 2)));
    }

    [Fact]
    public async Task FetchRangeAsync_ReturnsRatesGroupedByDate()
    {
        // Arrange
        string responseContent = """[{"date":"2025-01-01","base":"EUR","quote":"USD","rate":1.05},{"date":"2025-01-01","base":"EUR","quote":"CAD","rate":1.5},{"date":"2025-01-02","base":"EUR","quote":"USD","rate":1.06}]""";
        HttpClient httpClient = FakeHttpClient.CreateFakeHttpClient(responseContent, HttpStatusCode.OK);
        FrankfurterCurrencyConverter converter = new(httpClient);

        // Act
        IReadOnlyDictionary<DateOnly, Dictionary<string, decimal>> result = await converter.FetchRangeAsync(
            Currencies.EUR, new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 2));

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(1.05m, result[new DateOnly(2025, 1, 1)]["EURUSD"]);
        Assert.Equal(1.5m, result[new DateOnly(2025, 1, 1)]["EURCAD"]);
        Assert.Equal(1.06m, result[new DateOnly(2025, 1, 2)]["EURUSD"]);
    }

    [Fact]
    public async Task FetchRangeAsync_ThrowsArgumentException_WhenStartAfterEnd()
    {
        // Arrange
        HttpClient httpClient = FakeHttpClient.CreateFakeHttpClient("[]", HttpStatusCode.OK);
        FrankfurterCurrencyConverter converter = new(httpClient);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => converter.FetchRangeAsync(
            Currencies.EUR, new DateOnly(2025, 1, 5), new DateOnly(2025, 1, 1)));
    }

    [Fact]
    public async Task FetchAllRatesAsync_ExcludesBtcAndConfiguredCurrenciesFromRequest()
    {
        // Arrange
        CapturingHandler handler = new();
        HttpClient httpClient = new(handler);
        FrankfurterCurrencyConverter converter = new(httpClient, new[] { "ARS" });

        // Act
        await converter.FetchAllRatesAsync(Currencies.EUR, new DateTime(2025, 1, 2));

        // Assert
        Assert.NotNull(handler.LastUrl);
        Assert.DoesNotContain("BTC", handler.LastUrl);
        Assert.DoesNotContain("ARS", handler.LastUrl);
        Assert.Contains("USD", handler.LastUrl);
    }

    [Fact]
    public async Task FetchRangeAsync_UsesRequestedRangeAndBaseInUrl()
    {
        // Arrange
        CapturingHandler handler = new();
        HttpClient httpClient = new(handler);
        FrankfurterCurrencyConverter converter = new(httpClient);

        // Act
        await converter.FetchRangeAsync(Currencies.EUR, new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 3));

        // Assert
        Assert.NotNull(handler.LastUrl);
        Assert.Contains("from=2025-01-01", handler.LastUrl);
        Assert.Contains("to=2025-01-03", handler.LastUrl);
        Assert.Contains("base=EUR", handler.LastUrl);
        Assert.Contains("quotes=", handler.LastUrl);
        string[] quoteCodes = handler.LastUrl.Split("quotes=")[1].Split(',');
        Assert.DoesNotContain("EUR", quoteCodes);
        Assert.DoesNotContain("BTC", quoteCodes);
    }

    [Fact]
    public async Task FetchRangeAsync_IgnoresRatesWhereQuoteIsTheSource()
    {
        // Arrange
        string responseContent = """[{"date":"2025-01-02","base":"EUR","quote":"EUR","rate":1},{"date":"2025-01-02","base":"EUR","quote":"USD","rate":1.07}]""";
        HttpClient httpClient = FakeHttpClient.CreateFakeHttpClient(responseContent, HttpStatusCode.OK);
        FrankfurterCurrencyConverter converter = new(httpClient);

        // Act
        IReadOnlyDictionary<DateOnly, Dictionary<string, decimal>> result = await converter.FetchRangeAsync(
            Currencies.EUR, new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 2));

        // Assert
        Assert.Single(result);
        Assert.DoesNotContain("EUREUR", result[new DateOnly(2025, 1, 2)]);
        Assert.Equal(1.07m, result[new DateOnly(2025, 1, 2)]["EURUSD"]);
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public string? LastUrl { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            this.LastUrl = request.RequestUri?.ToString();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]"),
            });
        }
    }
}
