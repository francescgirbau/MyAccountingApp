using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Xunit;

namespace MyAccountingApp.Api.Tests;

public class ValidationEndpointsTests
{
    [Fact]
    public async Task Validate_ShouldReturnEntityIdsAndDeepLink()
    {
        // Arrange
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();

        string body = """
            {
              "transactions": [
                {
                  "id": "11111111-1111-1111-1111-111111111111",
                  "date": "2025-01-10T00:00:00",
                  "description": "Salary",
                  "money": { "amount": 1000, "currency": "EUR" },
                  "category": 1
                },
                {
                  "id": "22222222-2222-2222-2222-222222222222",
                  "date": "2025-01-10T00:00:00",
                  "description": "Salary",
                  "money": { "amount": 1000, "currency": "EUR" },
                  "category": 1
                }
              ],
              "assetTransactions": [],
              "optionTransactions": []
            }
            """;
        HttpResponseMessage seeded = await client.PostAsync("/api/backup", new StringContent(body, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.OK, seeded.StatusCode);

        // Act
        HttpResponseMessage response = await client.GetAsync("/api/validate");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement errors = doc.RootElement.GetProperty("errors");

        JsonElement? duplicate = null;
        foreach (JsonElement issue in errors.EnumerateArray())
        {
            if (issue.GetProperty("field").GetString() == "DUPLICATE_FINGERPRINT")
            {
                duplicate = issue;
                break;
            }
        }

        Assert.NotNull(duplicate);
        JsonElement entityIds = duplicate.Value.GetProperty("entityIds");
        Assert.Equal(2, entityIds.GetArrayLength());
        Assert.Equal("11111111-1111-1111-1111-111111111111", entityIds[0].GetString());
        Assert.Equal("22222222-2222-2222-2222-222222222222", entityIds[1].GetString());
        Assert.Equal("Transaction", duplicate.Value.GetProperty("entityType").GetString());
        Assert.True(duplicate.Value.GetProperty("deepLink").GetString()?.StartsWith("/transactions?ids="));
    }
}