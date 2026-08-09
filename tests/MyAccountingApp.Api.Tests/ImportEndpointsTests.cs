using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace MyAccountingApp.Api.Tests;

public class ImportEndpointsTests
{
    [Fact]
    public async Task Import_ShouldReturnOk_WhenFoldersMissing()
    {
        // Arrange
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/import", new { folderPaths = new[] { "/nonexistent/folder" } });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task RawCsv_ShouldReturnBadRequest_WhenFileEmpty()
    {
        // Arrange
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();
        using MultipartFormDataContent content = new();
        content.Add(new StringContent(string.Empty), "file", "test.csv");

        // Act
        HttpResponseMessage response = await client.PostAsync("/api/import/raw-csv", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RawCsv_ShouldImportValidRows()
    {
        // Arrange
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();
        string csv = "Date,Description,Amount,Type\n2026-08-01,Test expense,10.5,Expense\n";
        using MultipartFormDataContent content = new();
        content.Add(new StringContent(csv, Encoding.UTF8, "text/csv"), "file", "test.csv");

        // Act
        HttpResponseMessage response = await client.PostAsync("/api/import/raw-csv", content);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(1, document.RootElement.GetProperty("imported").GetInt32());
    }

    [Fact]
    public async Task DataReset_ShouldClearStores()
    {
        // Arrange
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.PostAsync("/api/data/reset", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(0, document.RootElement.GetProperty("clearedTransactions").GetInt32());
    }

    [Fact]
    public async Task SymbolLookup_ShouldReturnBadRequest_WhenNameEmpty()
    {
        // Arrange
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/api/symbol-lookup?name=");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
