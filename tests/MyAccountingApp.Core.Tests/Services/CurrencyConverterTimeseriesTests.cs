using System.Net;
using MyAccountingApp.Core.Services;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Exceptions;
using MyAccountingApp.TestUtilities.Fakes;

namespace MyAccountingApp.Core.Tests.Services;

public class CurrencyConverterTimeseriesTests
{
    [Fact]
    public async Task FetchRangeAsync_ReturnsRatesForEachDate()
    {
        // Arrange
        string responseContent = """{"success":true,"rates":{"2025-01-01":{"USD":1.05,"CAD":1.5},"2025-01-02":{"USD":1.06,"CAD":1.51}}}""";
        HttpClient client = FakeHttpClient.CreateFakeHttpClient(responseContent, HttpStatusCode.OK);
        CurrencyConverter converter = new("key", client);

        // Act
        IReadOnlyDictionary<DateOnly, Dictionary<string, decimal>> result = await converter.FetchRangeAsync(
            Currencies.EUR, new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 2));

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(1.05m, result[new DateOnly(2025, 1, 1)]["EURUSD"]);
        Assert.Equal(1.06m, result[new DateOnly(2025, 1, 2)]["EURUSD"]);
        Assert.Equal(1.51m, result[new DateOnly(2025, 1, 2)]["EURCAD"]);
    }

    [Fact]
    public async Task FetchRangeAsync_ThrowsArgumentException_WhenStartAfterEnd()
    {
        // Arrange
        HttpClient client = FakeHttpClient.CreateFakeHttpClient("""{"success":true,"rates":{}}""", HttpStatusCode.OK);
        CurrencyConverter converter = new("key", client);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => converter.FetchRangeAsync(
            Currencies.EUR, new DateOnly(2025, 1, 5), new DateOnly(2025, 1, 1)));
    }

    [Fact]
    public async Task FetchRangeAsync_ThrowsQuotaExceeded_WhenApiReportsQuotaError()
    {
        // Arrange
        string responseContent = """{"success":false,"error":{"code":104,"info":"Your monthly API request volume limit has been reached."}}""";
        HttpClient client = FakeHttpClient.CreateFakeHttpClient(responseContent, HttpStatusCode.OK);
        CurrencyConverter converter = new("key", client);

        // Act & Assert
        await Assert.ThrowsAsync<CurrencyApiQuotaExceededException>(() => converter.FetchRangeAsync(
            Currencies.EUR, new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 2)));
    }

    [Fact]
    public async Task FetchRangeAsync_ThrowsQuotaExceeded_WhenHttp429()
    {
        // Arrange
        HttpClient client = FakeHttpClient.CreateFakeHttpClient("{}", HttpStatusCode.TooManyRequests);
        CurrencyConverter converter = new("key", client);

        // Act & Assert
        await Assert.ThrowsAsync<CurrencyApiQuotaExceededException>(() => converter.FetchRangeAsync(
            Currencies.EUR, new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 2)));
    }

    [Fact]
    public async Task FetchRangeAsync_ExcludesConfiguredCurrenciesFromRequest()
    {
        // Arrange
        CapturingHandler handler = new();
        HttpClient client = new(handler);
        CurrencyConverter converter = new("key", client, new[] { "BTC" });

        // Act
        await converter.FetchRangeAsync(Currencies.EUR, new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 2));

        // Assert
        Assert.NotNull(handler.LastUrl);
        Assert.DoesNotContain("BTC", handler.LastUrl);
        Assert.Contains("USD", handler.LastUrl);
    }

    [Fact]
    public async Task FetchAllRatesAsync_ExcludesConfiguredCurrenciesFromRequest()
    {
        // Arrange
        CapturingHandler handler = new();
        HttpClient client = new(handler);
        CurrencyConverter converter = new("key", client, new[] { "BTC" });

        // Act
        await converter.FetchAllRatesAsync(Currencies.EUR, new DateTime(2025, 1, 1));

        // Assert
        Assert.NotNull(handler.LastUrl);
        Assert.DoesNotContain("BTC", handler.LastUrl);
        Assert.Contains("USD", handler.LastUrl);
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public string? LastUrl { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            this.LastUrl = request.RequestUri?.ToString();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"success":true,"rates":{}}"""),
            });
        }
    }
}
