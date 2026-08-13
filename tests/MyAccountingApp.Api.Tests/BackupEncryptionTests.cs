using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MyAccountingApp.Core.Vault;
using Xunit;

namespace MyAccountingApp.Api.Tests;

public class BackupEncryptionTests
{
    [Fact]
    public async Task Backup_ShouldBeEncrypted_WhenVaultIsUnlocked()
    {
        // Arrange
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/setup", new { password = "testpassword123" });
        await client.PostAsJsonAsync("/api/transactions", new
        {
            date = DateTime.Today,
            description = "Test transaction",
            amount = 12.5m,
            currency = "EUR",
            category = "EXPENSE",
        });

        // Act
        HttpResponseMessage response = await client.GetAsync("/api/backup");

        // Assert: binary payload, not plaintext JSON
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/octet-stream", response.Content.Headers.ContentType?.MediaType);
        byte[] bytes = await response.Content.ReadAsByteArrayAsync();
        string raw = Encoding.UTF8.GetString(bytes);
        Assert.DoesNotContain("transactions", raw);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<JsonElement>(raw));

        // Act 2: the encrypted blob can be restored as-is
        HttpResponseMessage restored = await client.PostAsync("/api/backup", new ByteArrayContent(bytes));
        Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
        string? message = JsonDocument.Parse(await restored.Content.ReadAsStringAsync()).RootElement.GetProperty("message").GetString();
        Assert.NotNull(message);
        Assert.Contains("Restored 1 transactions", message);
    }

    [Fact]
    public async Task Backup_ShouldStillAccept_PlaintextBackups_WhenVaultIsUnlocked()
    {
        // Arrange
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/setup", new { password = "testpassword123" });
        const string body = """{"transactions":[],"assetTransactions":[],"optionTransactions":[]}""";

        // Act: upload an old plaintext backup
        HttpResponseMessage response = await client.PostAsync("/api/backup", new StringContent(body, Encoding.UTF8, "application/json"));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Backup_ShouldReturn401_WhileVaultIsLocked()
    {
        // Arrange
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/setup", new { password = "testpassword123" });
        await client.PostAsJsonAsync("/api/auth/lock", new { });

        // Act
        HttpResponseMessage response = await client.GetAsync("/api/backup");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Backup_ShouldRejectGarbage_AndNotOverwriteData_WhenVaultIsUnlocked()
    {
        // Arrange
        using ApiWebApplicationFactory factory = new ApiWebApplicationFactory();
        HttpClient client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/setup", new { password = "testpassword123" });
        await client.PostAsJsonAsync("/api/transactions", new
        {
            date = DateTime.Today,
            description = "Keep me",
            amount = 12.5m,
            currency = "EUR",
            category = "EXPENSE",
        });

        // Act: upload an encrypted blob that does not decrypt (other vault / corrupted)
        HttpResponseMessage response = await client.PostAsync("/api/backup", new ByteArrayContent(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }));

        // Assert: rejected with the clear message and data untouched
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        string error = await response.Content.ReadAsStringAsync();
        Assert.Contains("neither valid JSON nor a vault-encrypted backup", error);

        HttpResponseMessage txResp = await client.GetAsync("/api/transactions");
        using JsonDocument txDoc = JsonDocument.Parse(await txResp.Content.ReadAsStringAsync());
        Assert.Equal(1, txDoc.RootElement.GetArrayLength());
    }

    private sealed class DisabledVaultFactory : ApiWebApplicationFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IVaultService>();
                services.AddSingleton<IVaultService>(new DisabledVaultService());
            });
        }
    }

    [Fact]
    public async Task VaultDisabled_ShouldReportStatus_AndServeDataWithoutSetup()
    {
        // Arrange
        using DisabledVaultFactory factory = new DisabledVaultFactory();
        HttpClient client = factory.CreateClient();

        // Act
        HttpResponseMessage statusResp = await client.GetAsync("/api/auth/status");
        HttpResponseMessage txResp = await client.GetAsync("/api/transactions");

        // Assert
        Assert.Equal(HttpStatusCode.OK, statusResp.StatusCode);
        using (JsonDocument doc = JsonDocument.Parse(await statusResp.Content.ReadAsStringAsync()))
        {
            Assert.False(doc.RootElement.GetProperty("isEnabled").GetBoolean());
        }

        Assert.Equal(HttpStatusCode.OK, txResp.StatusCode); // no setup, no unlock needed
    }
}