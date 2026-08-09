using MyAccountingApp.Core.Persistence;
using MyAccountingApp.Domain.Entities;

namespace MyAccountingApp.Core.Tests.Repositories;

public class JsonApiQuotaRepositoryTests : IDisposable
{
    private readonly string _tempFile;

    public JsonApiQuotaRepositoryTests()
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
    public void Get_ReturnsDefaultQuota_WhenFileDoesNotExist()
    {
        // Arrange
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        JsonApiQuotaRepository repo = new(path);

        // Act
        ApiUsageQuota quota = repo.Get();

        // Assert
        Assert.Equal("exchangerate.host", quota.Provider);
        Assert.Equal(100, quota.RequestsLimit);
        Assert.Equal(0, quota.RequestsUsed);
        Assert.Equal(90, quota.Available);
    }

    [Fact]
    public void Save_ThenGet_RoundTrips()
    {
        // Arrange
        JsonApiQuotaRepository repo = new(this._tempFile);
        DateOnly start = new(2026, 7, 1);
        ApiUsageQuota quota = new("exchangerate.host", start, start.AddMonths(1).AddDays(-1), 25, 100, 10, DateTime.UtcNow);

        // Act
        repo.Save(quota);
        ApiUsageQuota loaded = repo.Get();

        // Assert
        Assert.Equal(25, loaded.RequestsUsed);
        Assert.Equal(65, loaded.Available);
        Assert.Equal(start, loaded.PeriodStart);
    }
}
