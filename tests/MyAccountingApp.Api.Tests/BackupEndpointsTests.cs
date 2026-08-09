using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace MyAccountingApp.Api.Tests;

public class BackupEndpointsTests
{
    [Fact]
    public async Task Backup_ShouldExportJson()
    {
        // Arrange
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/api/backup");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(document.RootElement.TryGetProperty("transactions", out _));
    }

    [Fact]
    public async Task Backup_ShouldRestore_WhenBodyValid()
    {
        // Arrange
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();
        string body = """{"transactions":[],"assetTransactions":[],"optionTransactions":[]}""";

        // Act
        HttpResponseMessage response = await client.PostAsync("/api/backup", new StringContent(body, Encoding.UTF8, "application/json"));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Contains("Restored", document.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Backup_ShouldReturnBadRequest_WhenTransactionsMissing()
    {
        // Arrange
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();
        string body = """{"assetTransactions":[]}""";

        // Act
        HttpResponseMessage response = await client.PostAsync("/api/backup", new StringContent(body, Encoding.UTF8, "application/json"));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Backup_ShouldReturnBadRequest_WhenJsonInvalid()
    {
        // Arrange
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.PostAsync("/api/backup", new StringContent("not json", Encoding.UTF8, "application/json"));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
