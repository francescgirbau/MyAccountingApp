using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MyAccountingApp.Core.Persistence;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;
using MyAccountingApp.Domain.Interfaces;

namespace MyAccountingApp.Api.Tests;

public class FrankfurterEndpointsTests
{
    private static HttpClient CreateClientWithCachedRates()
    {
        InMemoryConversionRepository repo = new();
        repo.AddOrUpdate(new Conversion(
            new DateTime(2026, 8, 1),
            Currencies.EUR,
            new Dictionary<Currencies, decimal> { { Currencies.USD, 1.1m }, { Currencies.CAD, 1.5m } },
            sourceProvider: "frankfurter"));
        repo.AddOrUpdate(new Conversion(
            new DateTime(2026, 8, 2),
            Currencies.EUR,
            new Dictionary<Currencies, decimal> { { Currencies.USD, 1.12m }, { Currencies.CAD, 1.52m } },
            sourceProvider: "frankfurter"));

        ApiWebApplicationFactory factory = new();
        return factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IConversionRepository>();
            services.AddSingleton<IConversionRepository>(repo);
        })).CreateClient();
    }

    [Fact]
    public async Task Rates_ForCachedDate_ReturnsFrankfurterRecords()
    {
        // Arrange
        using HttpClient client = CreateClientWithCachedRates();

        // Act
        HttpResponseMessage response = await client.GetAsync("/api/frankfurter/v2/rates?date=2026-08-01");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        List<JsonElement> records = document.RootElement.EnumerateArray().ToList();
        Assert.Equal(2, records.Count);
        JsonElement cad = Assert.Single(records, r => r.GetProperty("quote").GetString() == "CAD");
        Assert.Equal("2026-08-01", cad.GetProperty("date").GetString());
        Assert.Equal("EUR", cad.GetProperty("base").GetString());
        Assert.Equal(1.5m, cad.GetProperty("rate").GetDecimal());
    }

    [Fact]
    public async Task Rates_ForNonEurBase_DerivesCrossRates()
    {
        // Arrange
        using HttpClient client = CreateClientWithCachedRates();

        // Act
        HttpResponseMessage response = await client.GetAsync("/api/frankfurter/v2/rates?date=2026-08-01&base=USD");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement cad = Assert.Single(document.RootElement.EnumerateArray());
        Assert.Equal("USD", cad.GetProperty("base").GetString());
        Assert.Equal("CAD", cad.GetProperty("quote").GetString());
        Assert.Equal(1.5m / 1.1m, cad.GetProperty("rate").GetDecimal());
    }

    [Fact]
    public async Task Rates_WithQuotesFilter_ReturnsOnlyRequestedQuotes()
    {
        // Arrange
        using HttpClient client = CreateClientWithCachedRates();

        // Act
        HttpResponseMessage response = await client.GetAsync("/api/frankfurter/v2/rates?date=2026-08-01&quotes=USD");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement usd = Assert.Single(document.RootElement.EnumerateArray());
        Assert.Equal("USD", usd.GetProperty("quote").GetString());
        Assert.Equal(1.1m, usd.GetProperty("rate").GetDecimal());
    }

    [Fact]
    public async Task Rates_ForMissingDate_ReturnsNotFound()
    {
        // Arrange
        using HttpClient client = CreateClientWithCachedRates();

        // Act
        HttpResponseMessage response = await client.GetAsync("/api/frankfurter/v2/rates?date=2020-01-01");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(document.RootElement.TryGetProperty("message", out _));
    }

    [Fact]
    public async Task Rates_UnknownBase_ReturnsBadRequest()
    {
        // Arrange
        using HttpClient client = CreateClientWithCachedRates();

        // Act
        HttpResponseMessage response = await client.GetAsync("/api/frankfurter/v2/rates?date=2026-08-01&base=XXXX");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Rates_UnknownQuote_ReturnsBadRequest()
    {
        // Arrange
        using HttpClient client = CreateClientWithCachedRates();

        // Act
        HttpResponseMessage response = await client.GetAsync("/api/frankfurter/v2/rates?date=2026-08-01&quotes=XXXX");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Rates_Range_ReturnsAllCachedDays()
    {
        // Arrange
        using HttpClient client = CreateClientWithCachedRates();

        // Act
        HttpResponseMessage response = await client.GetAsync("/api/frankfurter/v2/rates?from=2026-08-01&to=2026-08-05");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        List<JsonElement> records = document.RootElement.EnumerateArray().ToList();
        Assert.Equal(4, records.Count);
        Assert.Equal(2, records.Count(r => r.GetProperty("date").GetString() == "2026-08-02"));
    }

    [Fact]
    public async Task Rates_DateAndRangeCombined_ReturnsBadRequest()
    {
        // Arrange
        using HttpClient client = CreateClientWithCachedRates();

        // Act
        HttpResponseMessage response = await client.GetAsync("/api/frankfurter/v2/rates?date=2026-08-01&from=2026-08-01&to=2026-08-02");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}