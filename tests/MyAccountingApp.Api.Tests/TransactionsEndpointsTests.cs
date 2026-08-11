using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace MyAccountingApp.Api.Tests;

public class TransactionsEndpointsTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static object CreateTransactionBody(DateTime date, string category = "EXPENSE", decimal amount = 12.5m)
    {
        return new
        {
            date,
            description = "Test transaction",
            amount,
            currency = "EUR",
            category,
        };
    }

    [Fact]
    public async Task Transactions_ShouldRoundTrip()
    {
        // Arrange
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();
        DateTime date = new(2026, 8, 1, 10, 0, 0);

        // Act
        HttpResponseMessage created = await client.PostAsJsonAsync("/api/transactions", CreateTransactionBody(date));

        // Assert
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        string? location = created.Headers.Location?.ToString();
        Assert.NotNull(location);

        HttpResponseMessage list = await client.GetAsync("/api/transactions");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        using JsonDocument listDocument = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        Assert.Single(listDocument.RootElement.EnumerateArray());

        HttpResponseMessage updated = await client.PutAsJsonAsync(location, CreateTransactionBody(date, "INCOME", 20m));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        using JsonDocument updatedDocument = JsonDocument.Parse(await updated.Content.ReadAsStringAsync());
        Assert.Equal("INCOME", updatedDocument.RootElement.GetProperty("category").GetString());

        HttpResponseMessage deleted = await client.DeleteAsync(location!);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        HttpResponseMessage afterDelete = await client.GetAsync("/api/transactions");
        using JsonDocument afterDeleteDocument = JsonDocument.Parse(await afterDelete.Content.ReadAsStringAsync());
        Assert.Empty(afterDeleteDocument.RootElement.EnumerateArray());
    }

    [Fact]
    public async Task Transactions_ShouldReturnNotFound_WhenDeletingMissing()
    {
        // Arrange
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.DeleteAsync("/api/transactions/" + Guid.NewGuid());

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Transactions_YearDelete_ShouldReturnCounts()
    {
        // Arrange
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/transactions", CreateTransactionBody(new DateTime(2026, 8, 1)));

        // Act
        HttpResponseMessage response = await client.DeleteAsync("/api/transactions/year/2026");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(1, document.RootElement.GetProperty("deletedTransactions").GetInt32());
    }

    [Fact]
    public async Task Transactions_YearCount_ShouldCountEachStore()
    {
        // Arrange
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/transactions", CreateTransactionBody(new DateTime(2026, 8, 1)));

        // Act
        HttpResponseMessage response = await client.GetAsync("/api/transactions/year/2026/count");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(1, document.RootElement.GetProperty("transactions").GetInt32());
    }

    [Fact]
    public async Task BatchPatch_ShouldUpdateCategories_ForAllIds()
    {
        // Arrange
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();
        List<Guid> ids = new();
        for (int i = 0; i < 3; i++)
        {
            HttpResponseMessage created = await client.PostAsJsonAsync("/api/transactions", CreateTransactionBody(new DateTime(2026, 8, 1)));
            string location = created.Headers.Location!.ToString();
            ids.Add(Guid.Parse(location.Split('/').Last()));
        }

        // Act
        HttpResponseMessage response = await client.PatchAsJsonAsync(
            "/api/transactions/batch",
            new { ids, patch = new { category = "TRANSFER" } });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(3, document.RootElement.GetProperty("requested").GetInt32());
        Assert.Equal(3, document.RootElement.GetProperty("updated").GetInt32());
        Assert.Empty(document.RootElement.GetProperty("failures").EnumerateArray());

        HttpResponseMessage all = await client.GetAsync("/api/transactions");
        using JsonDocument allDocument = JsonDocument.Parse(await all.Content.ReadAsStringAsync());
        Assert.All(allDocument.RootElement.EnumerateArray(), tx => Assert.Equal("TRANSFER", tx.GetProperty("category").GetString()));
    }

    [Fact]
    public async Task BatchPatch_ShouldReportInvalidCategory_AsFailure()
    {
        // Arrange
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();
        HttpResponseMessage created = await client.PostAsJsonAsync("/api/transactions", CreateTransactionBody(new DateTime(2026, 8, 1)));
        string location = created.Headers.Location!.ToString();
        Guid id = Guid.Parse(location.Split('/').Last());

        // Act
        HttpResponseMessage response = await client.PatchAsJsonAsync(
            "/api/transactions/batch",
            new { ids = new[] { id }, patch = new { category = "NOT_A_CATEGORY" } });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(0, document.RootElement.GetProperty("updated").GetInt32());
        JsonElement failure = Assert.Single(document.RootElement.GetProperty("failures").EnumerateArray());
        Assert.Equal(id, failure.GetProperty("id").GetGuid());
        Assert.Contains("Invalid category", failure.GetProperty("error").GetString());
    }

    [Fact]
    public async Task BatchPatch_EmptyIds_ShouldReturnBadRequest()
    {
        // Arrange
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.PatchAsJsonAsync(
            "/api/transactions/batch",
            new { ids = Array.Empty<Guid>(), patch = new { category = "TRANSFER" } });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
