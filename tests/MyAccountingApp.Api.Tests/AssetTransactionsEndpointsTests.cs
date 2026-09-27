using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace MyAccountingApp.Api.Tests;

public class AssetTransactionsEndpointsTests
{
    private static object CreateAssetTransactionBody(DateTime date, string type = "Buy", decimal amount = 100m, decimal quantity = 2m, string symbol = "AAPL")
    {
        return new
        {
            date,
            description = "Test asset",
            amount,
            currency = "EUR",
            category = "EXPENSE",
            symbol,
            quantity,
            type,
        };
    }

    [Fact]
    public async Task AssetTransactions_ShouldRoundTrip()
    {
        // Arrange
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();
        DateTime date = new(2026, 8, 1, 10, 0, 0);

        // Act
        HttpResponseMessage created = await client.PostAsJsonAsync("/api/asset-transactions", CreateAssetTransactionBody(date));

        // Assert
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        string? location = created.Headers.Location?.ToString();
        Assert.NotNull(location);

        HttpResponseMessage bySymbol = await client.GetAsync("/api/asset-transactions/AAPL");
        Assert.Equal(HttpStatusCode.OK, bySymbol.StatusCode);
        using JsonDocument bySymbolDocument = JsonDocument.Parse(await bySymbol.Content.ReadAsStringAsync());
        Assert.Single(bySymbolDocument.RootElement.EnumerateArray());

        HttpResponseMessage updated = await client.PutAsJsonAsync(location, CreateAssetTransactionBody(date, "Sell", 200m, 3m));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        using JsonDocument updatedDocument = JsonDocument.Parse(await updated.Content.ReadAsStringAsync());
        Assert.Equal("Sell", updatedDocument.RootElement.GetProperty("type").GetString());

        HttpResponseMessage deleted = await client.DeleteAsync(location!);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        HttpResponseMessage afterDelete = await client.GetAsync("/api/asset-transactions");
        using JsonDocument afterDeleteDocument = JsonDocument.Parse(await afterDelete.Content.ReadAsStringAsync());
        Assert.Empty(afterDeleteDocument.RootElement.EnumerateArray());
    }

    [Fact]
    public async Task AssetTransactions_ShouldReturnNotFound_WhenDeletingMissing()
    {
        // Arrange
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.DeleteAsync("/api/asset-transactions/" + Guid.NewGuid());

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AssetTransactions_YearCount_ShouldCountPortfolio()
    {
        // Arrange
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/asset-transactions", CreateAssetTransactionBody(new DateTime(2026, 8, 1)));

        // Act
        HttpResponseMessage response = await client.GetAsync("/api/asset-transactions/year/2026/count");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(1, document.RootElement.GetProperty("assets").GetInt32());
    }

    [Fact]
    public async Task BatchPatch_ShouldUpdateSymbols_ForAllIds()
    {
        // Arrange
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();
        List<Guid> ids = new();
        for (int i = 0; i < 3; i++)
        {
            HttpResponseMessage created = await client.PostAsJsonAsync("/api/asset-transactions", CreateAssetTransactionBody(new DateTime(2026, 8, 1), symbol: $"S{i}"));
            string location = created.Headers.Location!.ToString();
            ids.Add(Guid.Parse(location.Split('/').Last()));
        }

        // Act
        HttpResponseMessage response = await client.PatchAsJsonAsync(
            "/api/asset-transactions/batch",
            new { ids, patch = new { symbol = "COBAS_INTERNACIONAL_D" } });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(3, document.RootElement.GetProperty("requested").GetInt32());
        Assert.Equal(3, document.RootElement.GetProperty("updated").GetInt32());
        Assert.Empty(document.RootElement.GetProperty("failures").EnumerateArray());

        HttpResponseMessage all = await client.GetAsync("/api/asset-transactions");
        using JsonDocument allDocument = JsonDocument.Parse(await all.Content.ReadAsStringAsync());
        Assert.All(allDocument.RootElement.EnumerateArray(), tx => Assert.Equal("COBAS_INTERNACIONAL_D", tx.GetProperty("symbol").GetString()));
    }

    [Fact]
    public async Task BatchPatch_ShouldReportMissingId_AsFailure()
    {
        // Arrange
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();
        HttpResponseMessage created = await client.PostAsJsonAsync("/api/asset-transactions", CreateAssetTransactionBody(new DateTime(2026, 8, 1)));
        string location = created.Headers.Location!.ToString();
        Guid existing = Guid.Parse(location.Split('/').Last());
        Guid missing = Guid.NewGuid();

        // Act
        HttpResponseMessage response = await client.PatchAsJsonAsync(
            "/api/asset-transactions/batch",
            new { ids = new[] { existing, missing }, patch = new { symbol = "TSLA" } });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(2, document.RootElement.GetProperty("requested").GetInt32());
        Assert.Equal(1, document.RootElement.GetProperty("updated").GetInt32());
        JsonElement failure = Assert.Single(document.RootElement.GetProperty("failures").EnumerateArray());
        Assert.Equal(missing, failure.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task BatchPatch_EmptyIds_ShouldReturnBadRequest()
    {
        // Arrange
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.PatchAsJsonAsync(
            "/api/asset-transactions/batch",
            new { ids = Array.Empty<Guid>(), patch = new { symbol = "TSLA" } });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task BulkDelete_ShouldRemoveSelectedAssetTransactions()
    {
        // Arrange
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();
        List<Guid> ids = new();
        for (int i = 0; i < 3; i++)
        {
            HttpResponseMessage created = await client.PostAsJsonAsync("/api/asset-transactions", CreateAssetTransactionBody(new DateTime(2026, 8, 1), symbol: $"S{i}"));
            ids.Add(Guid.Parse(created.Headers.Location!.ToString().Split('/').Last()));
        }

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/asset-transactions/bulk-delete",
            new { ids = new[] { ids[0], ids[1] } });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(2, document.RootElement.GetProperty("requested").GetInt32());
        Assert.Equal(2, document.RootElement.GetProperty("deleted").GetInt32());
        Assert.Empty(document.RootElement.GetProperty("failures").EnumerateArray());

        HttpResponseMessage all = await client.GetAsync("/api/asset-transactions");
        using JsonDocument allDocument = JsonDocument.Parse(await all.Content.ReadAsStringAsync());
        JsonElement remaining = Assert.Single(allDocument.RootElement.EnumerateArray());
        Assert.Equal(ids[2], remaining.GetProperty("transaction").GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task BulkDelete_ShouldReportMissingId_AsFailure()
    {
        // Arrange
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();
        HttpResponseMessage created = await client.PostAsJsonAsync("/api/asset-transactions", CreateAssetTransactionBody(new DateTime(2026, 8, 1)));
        Guid id = Guid.Parse(created.Headers.Location!.ToString().Split('/').Last());
        Guid missing = Guid.NewGuid();

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/asset-transactions/bulk-delete",
            new { ids = new[] { id, missing } });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(1, document.RootElement.GetProperty("deleted").GetInt32());
        JsonElement failure = Assert.Single(document.RootElement.GetProperty("failures").EnumerateArray());
        Assert.Equal(missing, failure.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task BulkDelete_EmptyIds_ShouldReturnBadRequest()
    {
        // Arrange
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/asset-transactions/bulk-delete",
            new { ids = Array.Empty<Guid>() });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Split_ShouldApplyFactor_ToLotsBeforeAsOfDate()
    {
        // Arrange
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/asset-transactions", CreateAssetTransactionBody(new DateTime(2023, 2, 13), quantity: 1000, symbol: "CEQ"));
        await client.PostAsJsonAsync("/api/asset-transactions", CreateAssetTransactionBody(new DateTime(2023, 8, 10), quantity: 800, symbol: "CEQ"));

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/asset-transactions/split",
            new { symbol = "CEQ", factor = 0.2m, asOfDate = new DateTime(2023, 6, 30) });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(1, document.RootElement.GetProperty("requested").GetInt32());
        Assert.Equal(1, document.RootElement.GetProperty("updated").GetInt32());
        Assert.Empty(document.RootElement.GetProperty("failures").EnumerateArray());

        HttpResponseMessage all = await client.GetAsync("/api/asset-transactions");
        using JsonDocument allDocument = JsonDocument.Parse(await all.Content.ReadAsStringAsync());
        JsonElement early = allDocument.RootElement.EnumerateArray().Single(t => TransactionDateStartsWith(t, "2023-02"));
        Assert.Equal(200, early.GetProperty("quantity").GetDecimal());
        Assert.Equal(100, early.GetProperty("transaction").GetProperty("money").GetProperty("amount").GetDecimal());
        JsonElement later = allDocument.RootElement.EnumerateArray().Single(t => TransactionDateStartsWith(t, "2023-08"));
        Assert.Equal(800, later.GetProperty("quantity").GetDecimal());
    }

    [Fact]
    public async Task SplitPreview_ShouldReturnAffectedLots()
    {
        // Arrange
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/asset-transactions", CreateAssetTransactionBody(new DateTime(2023, 2, 13), quantity: 1000, amount: 90, symbol: "CEQ"));

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/asset-transactions/split/preview",
            new { symbol = "CEQ", factor = 0.2m, asOfDate = new DateTime(2023, 6, 30) });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("CEQ", document.RootElement.GetProperty("symbol").GetString());
        Assert.Equal(0.2, document.RootElement.GetProperty("factor").GetDouble());
        JsonElement item = Assert.Single(document.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(1000, item.GetProperty("quantityBefore").GetDecimal());
        Assert.Equal(200, item.GetProperty("quantityAfter").GetDecimal());
        Assert.Equal(90, item.GetProperty("amount").GetDecimal());
    }

    [Fact]
    public async Task Split_InvalidFactor_ShouldReturnBadRequest()
    {
        // Arrange
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/asset-transactions/split",
            new { symbol = "CEQ", factor = 0 });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Split_EmptySymbol_ShouldReturnBadRequest()
    {
        // Arrange
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/asset-transactions/split",
            new { symbol = " ", factor = 1 });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static bool TransactionDateStartsWith(JsonElement assetTransaction, string prefix)
    {
        string? raw = assetTransaction.GetProperty("transaction").GetProperty("date").GetString();
        return raw is not null && raw.StartsWith(prefix, StringComparison.Ordinal);
    }
}
