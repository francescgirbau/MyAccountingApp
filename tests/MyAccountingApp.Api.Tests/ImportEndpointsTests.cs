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
    public async Task Import_AbnUnknownCodeNoKeyword_FlagsNeedsReview()
    {
        // Arrange
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();
        string tempDir = Path.Combine(Path.GetTempPath(), $"abn_e2e_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string csv = "accountNumber,mutationcode,transactiondate,valuedate,startsaldo,endsaldo,amount,description\n"
            + "889774927,XYZ,20260110,20260110,0,0,300,\"SOME UNKNOWN DESCRIPTION\"";
        File.WriteAllText(Path.Combine(tempDir, "ABN_test.csv"), csv);

        try
        {
            // Act
            HttpResponseMessage response = await client.PostAsJsonAsync("/api/import", new { folderPaths = new[] { tempDir } });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using JsonDocument importDoc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(1, importDoc.RootElement.GetProperty("filesProcessed").GetInt32());

            HttpResponseMessage list = await client.GetAsync("/api/transactions");
            using JsonDocument doc = JsonDocument.Parse(await list.Content.ReadAsStringAsync());

            // Assert
            JsonElement tx = Assert.Single(doc.RootElement.EnumerateArray());
            Assert.Equal("SOME UNKNOWN DESCRIPTION", tx.GetProperty("description").GetString());
            Assert.Equal("INCOME", tx.GetProperty("category").GetString());
            Assert.True(tx.GetProperty("needsReview").GetBoolean());
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
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
