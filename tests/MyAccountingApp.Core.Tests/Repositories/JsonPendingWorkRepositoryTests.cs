using System.Text.Json;
using MyAccountingApp.Core.Persistence;
using MyAccountingApp.Domain.Constants;
using MyAccountingApp.Domain.Entities;
using MyAccountingApp.Domain.Enums;

namespace MyAccountingApp.Core.Tests.Repositories;

public class JsonPendingWorkRepositoryTests : IDisposable
{
    private readonly string _tempFile;

    public JsonPendingWorkRepositoryTests()
    {
        this._tempFile = Path.GetTempFileName();
    }

    public void Dispose()
    {
        if (File.Exists(this._tempFile))
        {
            File.Delete(this._tempFile);
        }
    }

    [Fact]
    public void GetAll_ReturnsEmpty_WhenFileDoesNotExist()
    {
        // Arrange
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        JsonPendingWorkRepository repo = new(path);

        // Act
        IEnumerable<PendingWorkRequest> result = repo.GetAll();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void AddOrUpdate_ThenGetAll_RoundTrips()
    {
        // Arrange
        JsonPendingWorkRepository repo = new(this._tempFile);
        PendingWorkRequest request = new(PendingWorkOperations.CurrencyRate, "{\"date\":\"2026-07-01\"}", DateTime.UtcNow);

        // Act
        repo.AddOrUpdate(request);
        IEnumerable<PendingWorkRequest> loaded = repo.GetAll();

        // Assert
        PendingWorkRequest item = Assert.Single(loaded);
        Assert.Equal(PendingWorkOperations.CurrencyRate, item.Operation);
        Assert.Equal(PendingStatus.Pending, item.Status);
        Assert.NotEqual(Guid.Empty, item.Id);
        Assert.Equal(0, item.Attempts);
        Assert.Null(item.NextRetryAtUtc);
    }

    [Fact]
    public void AddOrUpdate_SameId_UpdatesExisting()
    {
        // Arrange
        JsonPendingWorkRepository repo = new(this._tempFile);
        PendingWorkRequest request = new(PendingWorkOperations.CurrencyRate, "{\"date\":\"2026-07-01\"}", DateTime.UtcNow);
        repo.AddOrUpdate(request);

        // Act
        request.MarkFailed("boom", DateTime.UtcNow, TimeSpan.FromMinutes(5), maxAttempts: 3);
        repo.AddOrUpdate(request);

        // Assert
        PendingWorkRequest loaded = Assert.Single(repo.GetAll());
        Assert.Equal(PendingStatus.Failed, loaded.Status);
        Assert.Equal("boom", loaded.LastError);
        Assert.Equal(1, loaded.Attempts);
    }

    [Fact]
    public void GetAll_MigratesLegacyConversionFormat()
    {
        // Arrange
        string legacyJson = """[{"Date":"2026-07-01","Source":"EUR","RequestedAtUtc":"2026-07-02T10:00:00","Status":"Failed","ProcessedAtUtc":null,"LastError":"boom"}]""";
        File.WriteAllText(this._tempFile, legacyJson);
        JsonPendingWorkRepository repo = new(this._tempFile);

        // Act
        List<PendingWorkRequest> migrated = repo.GetAll().ToList();

        // Assert
        PendingWorkRequest item = Assert.Single(migrated);
        Assert.Equal(PendingWorkOperations.CurrencyRate, item.Operation);
        Assert.Equal(PendingStatus.Failed, item.Status);
        Assert.Equal("boom", item.LastError);
        Assert.Equal(new DateTime(2026, 7, 2, 10, 0, 0), item.RequestedAtUtc);
        Assert.Equal(0, item.Attempts);

        using JsonDocument payload = JsonDocument.Parse(item.PayloadJson);
        Assert.Equal("2026-07-01", payload.RootElement.GetProperty("date").GetString());

        // The file is rewritten to the generic format so the migration only runs once.
        using JsonDocument file = JsonDocument.Parse(File.ReadAllText(this._tempFile));
        Assert.True(file.RootElement[0].TryGetProperty("Operation", out _));
        Assert.False(file.RootElement[0].TryGetProperty("Date", out _));
    }
}