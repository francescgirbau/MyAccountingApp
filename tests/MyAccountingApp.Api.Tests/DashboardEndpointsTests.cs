using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace MyAccountingApp.Api.Tests;

public class DashboardEndpointsTests
{
    [Fact]
    public async Task Dashboard_ShouldReturnCashAndPortfolioSnapshot()
    {
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/transactions", new
        {
            date = new DateTime(2026, 1, 10),
            description = "Salary",
            amount = 1000m,
            currency = "EUR",
            category = "INCOME",
        });
        await client.PostAsJsonAsync("/api/transactions", new
        {
            date = new DateTime(2026, 8, 1),
            description = "Groceries",
            amount = 100m,
            currency = "EUR",
            category = "EXPENSE",
        });
        await client.PostAsJsonAsync("/api/asset-transactions", new
        {
            date = new DateTime(2026, 1, 5),
            description = "Buy AAPL",
            amount = 200m,
            currency = "EUR",
            category = "EXPENSE",
            symbol = "AAPL",
            quantity = 2m,
            type = "Buy",
        });

        HttpResponseMessage response = await client.GetAsync("/api/dashboard?asOf=2026-08-11");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement root = document.RootElement;

        Assert.Equal("2026-08-11", root.GetProperty("asOf").GetString());
        JsonElement cash = root.GetProperty("cash");
        Assert.Equal(1000, cash.GetProperty("incomeYtd").GetDecimal());
        Assert.Equal(100, cash.GetProperty("expenseYtd").GetDecimal());
        Assert.Equal(900, cash.GetProperty("netCashFlowYtd").GetDecimal());

        JsonElement portfolio = root.GetProperty("portfolio");
        Assert.Equal(200, portfolio.GetProperty("totalCostBasisEur").GetDecimal());
        Assert.Equal(0, portfolio.GetProperty("realizedGainLossYtdEur").GetDecimal());
        Assert.Equal(1, portfolio.GetProperty("openPositionCount").GetInt32());

        Assert.Empty(root.GetProperty("alerts").EnumerateArray());
    }

    [Fact]
    public async Task Dashboard_ShouldDefaultToToday_AndReturnOk()
    {
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/dashboard");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.NotNull(document.RootElement.GetProperty("asOf").GetString());
        Assert.Equal(0, document.RootElement.GetProperty("cash").GetProperty("incomeYtd").GetDecimal());
    }
}