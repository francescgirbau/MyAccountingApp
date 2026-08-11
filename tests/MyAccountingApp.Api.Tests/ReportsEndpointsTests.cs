using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace MyAccountingApp.Api.Tests;

public class ReportsEndpointsTests
{
    [Fact]
    public async Task RealizedGains_ShouldReturnYearSalesWithFifoCosts()
    {
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/asset-transactions", new
        {
            date = new DateTime(2024, 1, 15),
            description = "Buy AAPL",
            amount = 1000m,
            currency = "EUR",
            category = "INVESTMENT",
            symbol = "AAPL",
            quantity = 10m,
            type = "Buy",
        });
        await client.PostAsJsonAsync("/api/asset-transactions", new
        {
            date = new DateTime(2025, 6, 1),
            description = "Sell AAPL",
            amount = 1500m,
            currency = "EUR",
            category = "INCOME",
            symbol = "AAPL",
            quantity = 10m,
            type = "Sell",
        });

        HttpResponseMessage response = await client.GetAsync("/api/reports/realized-gains?year=2025");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(2025, document.RootElement.GetProperty("year").GetInt32());
        Assert.Equal(500m, document.RootElement.GetProperty("totalRealizedGainLoss").GetDecimal());
        JsonElement symbol = Assert.Single(document.RootElement.GetProperty("symbols").EnumerateArray());
        Assert.Equal("AAPL", symbol.GetProperty("symbol").GetString());
        Assert.Equal(10m, symbol.GetProperty("soldQuantity").GetDecimal());
        Assert.Equal(1500m, symbol.GetProperty("proceeds").GetDecimal());
        Assert.Equal(1000m, symbol.GetProperty("costBasis").GetDecimal());
        Assert.Equal(500m, symbol.GetProperty("realizedGainLoss").GetDecimal());
        JsonElement sale = Assert.Single(symbol.GetProperty("sales").EnumerateArray());
        Assert.Equal(500m, sale.GetProperty("realizedGainLoss").GetDecimal());
    }

    [Fact]
    public async Task RealizedGains_ShouldBeEmpty_WhenNoSalesInYear()
    {
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/asset-transactions", new
        {
            date = new DateTime(2024, 1, 15),
            description = "Buy AAPL",
            amount = 1000m,
            currency = "EUR",
            category = "INVESTMENT",
            symbol = "AAPL",
            quantity = 10m,
            type = "Buy",
        });

        HttpResponseMessage response = await client.GetAsync("/api/reports/realized-gains?year=2025");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Empty(document.RootElement.GetProperty("symbols").EnumerateArray());
        Assert.Equal(0m, document.RootElement.GetProperty("totalRealizedGainLoss").GetDecimal());
    }

    [Fact]
    public async Task Withholding_ShouldReturnPerCurrencyTotals()
    {
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/transactions", new
        {
            date = new DateTime(2025, 3, 1),
            description = "Withholding",
            amount = 15m,
            currency = "USD",
            category = "WITHHOLDING_TAX",
        });
        await client.PostAsJsonAsync("/api/transactions", new
        {
            date = new DateTime(2025, 4, 1),
            description = "Withholding",
            amount = 20m,
            currency = "USD",
            category = "WITHHOLDING_TAX",
        });

        HttpResponseMessage response = await client.GetAsync("/api/reports/withholding?year=2025");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(2025, document.RootElement.GetProperty("year").GetInt32());
        JsonElement total = Assert.Single(document.RootElement.GetProperty("totals").EnumerateArray());
        Assert.Equal("USD", total.GetProperty("currency").GetString());
        Assert.Equal(35m, total.GetProperty("amount").GetDecimal());
        Assert.Equal(2, total.GetProperty("transactionCount").GetInt32());
    }
}
