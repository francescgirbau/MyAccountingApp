using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Xunit;

namespace MyAccountingApp.Api.Tests;

public class DataQualityEndpointsTests
{
    private const string SeedBody = """
        {
          "transactions": [
            {
              "id": "11111111-1111-1111-1111-111111111111",
              "date": "2019-05-20T00:00:00",
              "description": "TARGETA *9027 Revolut top-up",
              "money": { "amount": 200, "currency": "EUR" },
              "category": 2
            },
            {
              "id": "22222222-2222-2222-2222-222222222222",
              "date": "2019-05-20T00:00:00",
              "description": "Top-up by *9027",
              "money": { "amount": 200, "currency": "EUR" },
              "category": 3
            }
          ],
          "assetTransactions": [],
          "optionTransactions": []
        }
        """;

    [Fact]
    public async Task Recalculate_ReturnsCounts()
    {
        // Arrange
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();
        HttpResponseMessage seeded = await client.PostAsync("/api/backup", new StringContent(SeedBody, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.OK, seeded.StatusCode);

        // Act
        HttpResponseMessage response = await client.PostAsync("/api/data-quality/transfer-matches/recalculate", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(1, document.RootElement.GetProperty("transferCount").GetInt32());
        Assert.Equal(1, document.RootElement.GetProperty("matchedPairs").GetInt32());
        Assert.Equal(0, document.RootElement.GetProperty("unmatchedTransfers").GetInt32());
        Assert.Equal(0, document.RootElement.GetProperty("changedTransactions").GetInt32());
        Assert.True(document.RootElement.TryGetProperty("calculatedAtUtc", out _));
    }

    [Fact]
    public async Task Recalculate_SecondRun_ChangesNothing()
    {
        // Arrange
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();
        await client.PostAsync("/api/backup", new StringContent(SeedBody, Encoding.UTF8, "application/json"));

        // Act
        await client.PostAsync("/api/data-quality/transfer-matches/recalculate", null);
        HttpResponseMessage response = await client.PostAsync("/api/data-quality/transfer-matches/recalculate", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(1, document.RootElement.GetProperty("transferCount").GetInt32());
        Assert.Equal(1, document.RootElement.GetProperty("matchedPairs").GetInt32());
        Assert.Equal(0, document.RootElement.GetProperty("unmatchedTransfers").GetInt32());
        Assert.Equal(0, document.RootElement.GetProperty("changedTransactions").GetInt32());
    }

    [Fact]
    public async Task Recalculate_NoTransactions_ReturnsZeros()
    {
        // Arrange
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.PostAsync("/api/data-quality/transfer-matches/recalculate", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(0, document.RootElement.GetProperty("transferCount").GetInt32());
        Assert.Equal(0, document.RootElement.GetProperty("matchedPairs").GetInt32());
        Assert.Equal(0, document.RootElement.GetProperty("unmatchedTransfers").GetInt32());
        Assert.Equal(0, document.RootElement.GetProperty("changedTransactions").GetInt32());
    }

    [Fact]
    public async Task Recalculate_LockedVault_Returns401()
    {
        // Arrange
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/setup", new { password = "testpassword123" });
        await client.PostAsJsonAsync("/api/auth/lock", new { });

        // Act
        HttpResponseMessage response = await client.PostAsync("/api/data-quality/transfer-matches/recalculate", null);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}