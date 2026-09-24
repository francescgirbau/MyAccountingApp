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

        // Operating YTD
        JsonElement operatingYtd = cash.GetProperty("operatingYtd");
        Assert.Equal(1000, operatingYtd.GetProperty("income").GetDecimal());
        Assert.Equal(100, operatingYtd.GetProperty("expenses").GetDecimal());
        Assert.Equal(900, operatingYtd.GetProperty("netOperatingCashFlow").GetDecimal());

        // Investing YTD
        JsonElement investingYtd = cash.GetProperty("investingYtd");
        Assert.Equal(200, investingYtd.GetProperty("purchases").GetDecimal());
        Assert.Equal(0, investingYtd.GetProperty("sales").GetDecimal());
        Assert.Equal(-200, investingYtd.GetProperty("netInvestedCash").GetDecimal());

        JsonElement portfolio = root.GetProperty("portfolio");
        Assert.Equal(200, portfolio.GetProperty("totalCostBasisEur").GetDecimal());
        Assert.Equal(0, portfolio.GetProperty("realizedGainLossYtdEur").GetDecimal());
        Assert.Equal(1, portfolio.GetProperty("openPositionCount").GetInt32());

        JsonElement alerts = root.GetProperty("alerts");
        Assert.Contains(alerts.EnumerateArray(), a =>
            a.GetProperty("code").GetString() == "DATA_QUALITY"
            && a.GetProperty("severity").GetString() == "warning"
            && a.GetProperty("link").GetString() == "/data-quality");
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
        Assert.Equal(0, document.RootElement.GetProperty("cash").GetProperty("operatingYtd").GetProperty("income").GetDecimal());
    }

    [Fact]
    public async Task Dashboard_ShowsLoanMovementsInOwnInternalBuckets()
    {
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();

        HttpResponseMessage created = await client.PostAsJsonAsync("/api/loans", new
        {
            startDate = new DateTime(2026, 3, 1),
            counterparty = "Berta",
            amount = 5000m,
            currency = "EUR",
            direction = "Borrowed",
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using JsonDocument createdDocument = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        Guid loanId = createdDocument.RootElement.GetProperty("loanId").GetGuid();

        HttpResponseMessage repayment = await client.PostAsJsonAsync($"/api/loans/{loanId}/repayments", new
        {
            date = new DateTime(2026, 6, 1),
            amount = 500m,
        });
        Assert.Equal(HttpStatusCode.OK, repayment.StatusCode);

        HttpResponseMessage response = await client.GetAsync("/api/dashboard?asOf=2026-08-11");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement internalYtd = document.RootElement.GetProperty("cash").GetProperty("internalYtd");

        // Loan movements are real cash in/out but they live in their own buckets,
        // not merged into Deposits/Transfers (which stay as inter-account moves only).
        Assert.Equal(5000, internalYtd.GetProperty("loanIn").GetDecimal());
        Assert.Equal(500, internalYtd.GetProperty("loanOut").GetDecimal());
        Assert.Equal(4500, internalYtd.GetProperty("loanNet").GetDecimal());
        Assert.Equal(0, internalYtd.GetProperty("deposits").GetDecimal());
        Assert.Equal(0, internalYtd.GetProperty("transfers").GetDecimal());
    }

    [Fact]
    public async Task Dashboard_FlagsManualTransactionThatDuplicatesLoanMovement()
    {
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();

        HttpResponseMessage created = await client.PostAsJsonAsync("/api/loans", new
        {
            startDate = new DateTime(2026, 3, 1),
            counterparty = "Berta",
            amount = 5000m,
            currency = "EUR",
            direction = "Borrowed",
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        HttpResponseMessage manual = await client.PostAsJsonAsync("/api/transactions", new
        {
            date = new DateTime(2026, 3, 1),
            description = "Manual duplicate of loan deposit",
            amount = 5000m,
            currency = "EUR",
            category = "DEPOSIT",
        });
        Assert.Equal(HttpStatusCode.Created, manual.StatusCode);

        HttpResponseMessage response = await client.GetAsync("/api/dashboard?asOf=2026-08-11");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement alerts = document.RootElement.GetProperty("alerts");

        Assert.Contains(alerts.EnumerateArray(), a =>
            a.GetProperty("code").GetString() == "LOAN_MANUAL_DUPLICATE"
            && a.GetProperty("severity").GetString() == "warning"
            && (a.GetProperty("link").GetString() ?? string.Empty).StartsWith("/transactions?ids="));
    }

    [Fact]
    public async Task Dashboard_DoesNotFlagManualTransactionWhenNoLoanMatch()
    {
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();

        HttpResponseMessage created = await client.PostAsJsonAsync("/api/loans", new
        {
            startDate = new DateTime(2026, 3, 1),
            counterparty = "Berta",
            amount = 5000m,
            currency = "EUR",
            direction = "Borrowed",
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        HttpResponseMessage manual = await client.PostAsJsonAsync("/api/transactions", new
        {
            date = new DateTime(2026, 3, 15),
            description = "Unrelated deposit",
            amount = 2500m,
            currency = "EUR",
            category = "DEPOSIT",
        });
        Assert.Equal(HttpStatusCode.Created, manual.StatusCode);

        HttpResponseMessage response = await client.GetAsync("/api/dashboard?asOf=2026-08-11");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement alerts = document.RootElement.GetProperty("alerts");

        Assert.DoesNotContain(alerts.EnumerateArray(), a =>
            a.GetProperty("code").GetString() == "LOAN_MANUAL_DUPLICATE");
    }
}