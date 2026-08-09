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
}
