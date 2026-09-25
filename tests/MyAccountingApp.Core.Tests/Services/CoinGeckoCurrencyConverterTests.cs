using System.Net;
using MyAccountingApp.Core.Http.Currency;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Exceptions;
using MyAccountingApp.TestUtilities.Fakes;

namespace MyAccountingApp.Core.Tests.Services;

public class CoinGeckoCurrencyConverterTests
{
    [Fact]
    public async Task FetchAllRatesAsync_ReturnsBtcQuote_WhenHistoryApiReturnsPrice()
    {
        // Arrange
        string responseContent = """{"market_data":{"current_price":{"eur":60000}}}""";
        HttpClient httpClient = FakeHttpClient.CreateFakeHttpClient(responseContent, HttpStatusCode.OK);
        CoinGeckoCurrencyConverter converter = new(httpClient);

        // Act
        Dictionary<string, decimal> result = await converter.FetchAllRatesAsync(Currencies.EUR, new DateTime(2025, 1, 2));

        // Assert
        Assert.Single(result);
        Assert.Equal(1m / 60000m, result["EURBTC"]);
    }

    [Fact]
    public async Task FetchAllRatesAsync_UsesDayFirstDateFormat_InHistoryUrl()
    {
        // Arrange
        CapturingHandler handler = new();
        HttpClient httpClient = new(handler);
        CoinGeckoCurrencyConverter converter = new(httpClient);

        // Act
        await converter.FetchAllRatesAsync(Currencies.EUR, new DateTime(2025, 1, 2));

        // Assert
        Assert.Contains("date=02-01-2025", handler.LastUrl);
    }

    [Fact]
    public async Task FetchAllRatesAsync_ReturnsEmpty_WhenPriceIsMissing()
    {
        // Arrange
        string responseContent = """{"market_data":{"current_price":{}}}""";
        HttpClient httpClient = FakeHttpClient.CreateFakeHttpClient(responseContent, HttpStatusCode.OK);
        CoinGeckoCurrencyConverter converter = new(httpClient);

        // Act
        Dictionary<string, decimal> result = await converter.FetchAllRatesAsync(Currencies.EUR, new DateTime(2025, 1, 2));

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task FetchAllRatesAsync_ThrowsException_WhenApiReturnsErrorMessage()
    {
        // Arrange
        string responseContent = """{"status":{"error_code":404,"error_message":"Could not find coin bitcoin"}}""";
        HttpClient httpClient = FakeHttpClient.CreateFakeHttpClient(responseContent, HttpStatusCode.NotFound);
        CoinGeckoCurrencyConverter converter = new(httpClient);

        // Act
        Exception ex = await Assert.ThrowsAsync<Exception>(() => converter.FetchAllRatesAsync(Currencies.EUR, new DateTime(2025, 1, 2)));

        // Assert
        Assert.Contains("Could not find coin bitcoin", ex.Message);
    }

    [Fact]
    public async Task FetchAllRatesAsync_ThrowsQuotaExceeded_WhenHttp429()
    {
        // Arrange
        HttpClient httpClient = FakeHttpClient.CreateFakeHttpClient("{}", HttpStatusCode.TooManyRequests);
        CoinGeckoCurrencyConverter converter = new(httpClient);

        // Act & Assert
        await Assert.ThrowsAsync<CurrencyApiQuotaExceededException>(() => converter.FetchAllRatesAsync(Currencies.EUR, new DateTime(2025, 1, 2)));
    }

    [Fact]
    public async Task FetchRangeAsync_ReturnsLastPointOfEachDay()
    {
        // Arrange
        string responseContent = """{"prices":[[1735689600000,60000],[1735732800000,61000],[1735776000000,62000]]}""";
        HttpClient httpClient = FakeHttpClient.CreateFakeHttpClient(responseContent, HttpStatusCode.OK);
        CoinGeckoCurrencyConverter converter = new(httpClient);

        // Act
        IReadOnlyDictionary<DateOnly, Dictionary<string, decimal>> result = await converter.FetchRangeAsync(
            Currencies.EUR, new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 2));

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(1m / 61000m, result[new DateOnly(2025, 1, 1)]["EURBTC"]);
        Assert.Equal(1m / 62000m, result[new DateOnly(2025, 1, 2)]["EURBTC"]);
    }

    [Fact]
    public async Task FetchRangeAsync_UsesUtcDates_InRangeUrl()
    {
        // Arrange
        CapturingHandler handler = new();
        HttpClient httpClient = new(handler);
        CoinGeckoCurrencyConverter converter = new(httpClient);

        // Act
        await converter.FetchRangeAsync(Currencies.EUR, new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 2));

        // Assert
        Assert.Contains("vs_currency=eur", handler.LastUrl);
        Assert.Contains($"from={new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds()}", handler.LastUrl);
        Assert.Contains($"to={new DateTimeOffset(2025, 1, 3, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds()}", handler.LastUrl);
    }

    [Fact]
    public async Task FetchRangeAsync_ReturnsEmpty_WhenTargetsExcludeBtc()
    {
        // Arrange
        HttpClient httpClient = FakeHttpClient.CreateFakeHttpClient("{}", HttpStatusCode.OK);
        CoinGeckoCurrencyConverter converter = new(httpClient);

        // Act
        IReadOnlyDictionary<DateOnly, Dictionary<string, decimal>> result = await converter.FetchRangeAsync(
            Currencies.EUR, new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 2), new[] { Currencies.USD });

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task FetchRangeAsync_ThrowsArgumentException_WhenStartAfterEnd()
    {
        // Arrange
        HttpClient httpClient = FakeHttpClient.CreateFakeHttpClient("{}", HttpStatusCode.OK);
        CoinGeckoCurrencyConverter converter = new(httpClient);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => converter.FetchRangeAsync(
            Currencies.EUR, new DateOnly(2025, 1, 3), new DateOnly(2025, 1, 2)));
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        /// <summary>
        /// Gets the URL of the last handled request.
        /// </summary>
        public string? LastUrl { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            this.LastUrl = request.RequestUri?.ToString();
            HttpResponseMessage response = new(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"market_data":{"current_price":{"eur":60000}}}"""),
            };
            return Task.FromResult(response);
        }
    }
}