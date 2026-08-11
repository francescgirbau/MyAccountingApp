using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MyAccountingApp.Api.Tests.Fakes;

namespace MyAccountingApp.Api.Tests;

public class PortfolioEndpointsTests
{
    [Fact]
    public async Task Portfolio_ShouldReturnEmpty_WhenNoPositions()
    {
        // Arrange
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/api/portfolio");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Empty(document.RootElement.EnumerateArray());
    }

    [Fact]
    public async Task Portfolio_ShouldReturnNotFound_WhenSymbolUnknown()
    {
        // Arrange
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/api/portfolio/AAPL");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Validate_ShouldReturnValid_WhenDataEmpty()
    {
        // Arrange
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/api/validate");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(document.RootElement.GetProperty("isValid").GetBoolean());
    }

    [Fact]
    public async Task Summary_ShouldReturnEmpty_WhenNoData()
    {
        // Arrange
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/api/summary");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Empty(document.RootElement.EnumerateArray());
    }

    [Fact]
    public async Task Summary_ShouldReturnNotFound_WhenYearMissing()
    {
        // Arrange
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/api/summary/2026");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Portfolio_ByDefault_DoesNotFetchMarketPrices()
    {
        // Arrange
        CountingMarketPriceService.Reset();
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();
        await SeedBuyAsync(client);

        // Act
        HttpResponseMessage response = await client.GetAsync("/api/portfolio");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, CountingMarketPriceService.Calls);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement position = Assert.Single(document.RootElement.EnumerateArray());
        Assert.Equal(JsonValueKind.Null, position.GetProperty("marketPrice").ValueKind);
        Assert.Equal(JsonValueKind.Null, position.GetProperty("unrealizedGainLoss").ValueKind);
    }

    [Fact]
    public async Task Portfolio_WithIncludePrices_FetchesMarketPrices()
    {
        // Arrange
        CountingMarketPriceService.Reset();
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();
        await SeedBuyAsync(client);

        // Act
        HttpResponseMessage response = await client.GetAsync("/api/portfolio?includePrices=true");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, CountingMarketPriceService.Calls);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement position = Assert.Single(document.RootElement.EnumerateArray());
        Assert.Equal(100m, position.GetProperty("marketPrice").GetDecimal());
        Assert.Equal(50m, position.GetProperty("unrealizedGainLoss").GetDecimal());
    }

    [Fact]
    public async Task Portfolio_SingleSymbol_FetchesMarketPriceByDefault()
    {
        // Arrange
        CountingMarketPriceService.Reset();
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();
        await SeedBuyAsync(client);

        // Act
        HttpResponseMessage response = await client.GetAsync("/api/portfolio/AAPL");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, CountingMarketPriceService.Calls);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(100m, document.RootElement.GetProperty("marketPrice").GetDecimal());
    }

    [Fact]
    public async Task Portfolio_SingleSymbol_WithoutPrices_DoesNotFetch()
    {
        // Arrange
        CountingMarketPriceService.Reset();
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();
        await SeedBuyAsync(client);

        // Act
        HttpResponseMessage response = await client.GetAsync("/api/portfolio/AAPL?includePrices=false");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, CountingMarketPriceService.Calls);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("marketPrice").ValueKind);
    }

    [Fact]
    public async Task RefreshPrices_ShouldWarmPricesAndReturnPositions()
    {
        // Arrange
        CountingMarketPriceService.Reset();
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();
        await SeedBuyAsync(client);

        // Act
        HttpResponseMessage response = await client.PostAsync("/api/portfolio/refresh-prices", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, CountingMarketPriceService.Calls);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement position = Assert.Single(document.RootElement.EnumerateArray());
        Assert.Equal(100m, position.GetProperty("marketPrice").GetDecimal());
    }

    private static async Task SeedBuyAsync(HttpClient client)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/asset-transactions", new
        {
            date = new DateTime(2026, 1, 5),
            description = "Buy AAPL",
            amount = 150m,
            currency = "EUR",
            category = "EXPENSE",
            symbol = "AAPL",
            quantity = 2m,
            type = "Buy",
        });
        response.EnsureSuccessStatusCode();
    }
}
